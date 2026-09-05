using System.Text.Json;
using System.Text.RegularExpressions;
using botFacturacion.Application.DTOs;
using botFacturacion.Application.Interfaces;
using botFacturacion.Domain.Entities;
using botFacturacion.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace botFacturacion.Application.Services;

/// <summary>
/// Servicio que gestiona el flujo conversacional para agendar y consultar citas médicas.
/// </summary>
public class CitaService
{
    private readonly IConversationRepository _conversationRepo;
    private readonly ICitaRepository _citaRepo;
    private readonly IWhatsAppService _whatsApp;
    private readonly IGoogleCalendarService _calendarService;
    private readonly ILogger<CitaService> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly Regex _emailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public CitaService(
        IConversationRepository conversationRepo,
        ICitaRepository citaRepo,
        IWhatsAppService whatsApp,
        IGoogleCalendarService calendarService,
        ILogger<CitaService> logger)
    {
        _conversationRepo = conversationRepo;
        _citaRepo = citaRepo;
        _whatsApp = whatsApp;
        _calendarService = calendarService;
        _logger = logger;
    }

    // ─── Máquina de estados ───────────────────────────────────────────────────

    public async Task HandleStepAsync(IncomingMessageDto mensaje, Domain.Entities.ConversationState estado)
    {
        try
        {
            switch (estado.CurrentStep)
            {
                case StepType.Cita_ConfirmarPerfil:
                    await ProcesarConfirmarPerfilAsync(mensaje, estado);
                    break;

                case StepType.Cita_Nombre:
                    await ProcesarNombreAsync(mensaje, estado);
                    break;

                case StepType.Cita_Telefono:
                    await ProcesarTelefonoAsync(mensaje, estado);
                    break;

                case StepType.Cita_TelefonoInput:
                    await ProcesarTelefonoInputAsync(mensaje, estado);
                    break;

                case StepType.Cita_Email:
                    await ProcesarEmailAsync(mensaje, estado);
                    break;

                case StepType.Cita_Horario:
                    await ProcesarHorarioAsync(mensaje, estado);
                    break;

                case StepType.Cita_Motivo:
                    await ProcesarMotivoAsync(mensaje, estado);
                    break;

                case StepType.Cita_Confirmacion:
                    await ProcesarConfirmacionAsync(mensaje, estado);
                    break;

                default:
                    _logger.LogWarning("Paso desconocido {Step} en flujo Cita para {Phone}",
                        estado.CurrentStep, mensaje.PhoneNumber);
                    await _conversationRepo.ResetAsync(mensaje.PhoneNumber);
                    await _whatsApp.SendTextAsync(mensaje.PhoneNumber,
                        "⚠️ Ocurrió un error en el flujo. Escribe *menu* para reiniciar.");
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en flujo Cita paso {Step} para {Phone}",
                estado.CurrentStep, mensaje.PhoneNumber);
            throw;
        }
    }

    // ─── Inicio del flujo: identificar si es usuario registrado ──────────────

    public async Task IniciarFlujoAsync(string phoneNumber)
    {
        var citaAnterior = (await _citaRepo.GetByPhoneAsync(phoneNumber))
            .OrderByDescending(c => c.CreadoEn)
            .FirstOrDefault();

        if (citaAnterior != null &&
            !string.IsNullOrWhiteSpace(citaAnterior.PacienteNombre) &&
            !string.IsNullOrWhiteSpace(citaAnterior.PacienteEmail))
        {
            var tel = citaAnterior.PacienteTelefonoContacto ?? string.Empty;
            var telFmt = tel.Length == 10 ? $"{tel[..3]}-{tel[3..6]}-{tel[6..]}" : tel;

            var tempData = new CitaTempData
            {
                PacienteNombre = citaAnterior.PacienteNombre,
                PacienteTelefono = citaAnterior.PacienteTelefonoContacto,
                PacienteEmail = citaAnterior.PacienteEmail
            };

            await _conversationRepo.UpdateAsync(
                phoneNumber, FlowType.AgendarCita, StepType.Cita_ConfirmarPerfil,
                JsonSerializer.Serialize(tempData));

            await _whatsApp.SendButtonsAsync(phoneNumber,
                $"👋 ¡Hola de nuevo, *{citaAnterior.PacienteNombre}*!\n\n" +
                $"Tenemos tus datos registrados:\n" +
                $"📞 {telFmt}\n" +
                $"📧 {citaAnterior.PacienteEmail}\n\n" +
                "¿Agendamos con estos datos?",
            [
                ("PERFIL_SI", "✅ Sí, usar estos datos"),
                ("PERFIL_NO", "✏️ Actualizar datos")
            ]);
        }
        else
        {
            await _conversationRepo.UpdateAsync(phoneNumber, FlowType.AgendarCita, StepType.Cita_Nombre);
            await _whatsApp.SendTextAsync(phoneNumber,
                "📅 *Agendar Cita*\n\n" +
                "Vamos a agendar tu cita. Puedes escribir *cancelar* en cualquier momento.\n\n" +
                "¿Cuál es el *nombre completo* del paciente?");
        }
    }

    // ─── Confirmar perfil (usuario recurrente) ────────────────────────────────

    private async Task ProcesarConfirmarPerfilAsync(IncomingMessageDto mensaje, Domain.Entities.ConversationState estado)
    {
        var respuesta = (mensaje.InteractiveId ?? mensaje.TextBody).Trim().ToUpperInvariant();

        if (respuesta is "PERFIL_SI" or "SI" or "SÍ" or "S")
        {
            var tempData = DeserializarTempData(estado.TempData);
            await _whatsApp.SendTextAsync(mensaje.PhoneNumber, "⏳ Consultando disponibilidad...");
            await DetectarYMostrarHorariosAsync(mensaje.PhoneNumber, tempData);
        }
        else
        {
            await _conversationRepo.UpdateAsync(mensaje.PhoneNumber, FlowType.AgendarCita, StepType.Cita_Nombre);
            await _whatsApp.SendTextAsync(mensaje.PhoneNumber,
                "¿Cuál es el *nombre completo* del paciente?");
        }
    }

    // ─── Paso 1: Nombre ───────────────────────────────────────────────────────

    private async Task ProcesarNombreAsync(IncomingMessageDto mensaje, Domain.Entities.ConversationState estado)
    {
        var nombre = mensaje.TextBody.Trim();
        var palabras = nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (palabras.Length < 2 || palabras.Any(p => p.Length < 2) || nombre.Length > 100)
        {
            await _whatsApp.SendTextAsync(mensaje.PhoneNumber,
                "⚠️ Por favor, ingresa el *nombre completo* con al menos un nombre y un apellido.\n\n" +
                "Ejemplo: _Juan García_");
            return;
        }

        var nombreCapitalizado = string.Join(" ", palabras.Select(p =>
            char.ToUpper(p[0]) + p[1..].ToLower()));

        var telWhatsApp = ExtraerDiezDigitos(mensaje.PhoneNumber);
        var telFmt = telWhatsApp.Length == 10
            ? $"{telWhatsApp[..3]}-{telWhatsApp[3..6]}-{telWhatsApp[6..]}"
            : telWhatsApp;

        var tempData = new CitaTempData
        {
            PacienteNombre = nombreCapitalizado,
            PacienteTelefono = telWhatsApp
        };

        await _conversationRepo.UpdateAsync(
            mensaje.PhoneNumber, FlowType.AgendarCita, StepType.Cita_Telefono,
            JsonSerializer.Serialize(tempData));

        await _whatsApp.SendButtonsAsync(mensaje.PhoneNumber,
            $"✅ Nombre: *{nombreCapitalizado}*\n\n" +
            $"Tu número de WhatsApp es *{telFmt}*.\n" +
            "¿Registramos la cita con este número de contacto?",
        [
            ("TEL_SI", "✅ Sí, usar este"),
            ("TEL_CAMBIAR", "✏️ Usar otro número")
        ]);
    }

    // ─── Paso 2: Confirmar teléfono ───────────────────────────────────────────

    private async Task ProcesarTelefonoAsync(IncomingMessageDto mensaje, Domain.Entities.ConversationState estado)
    {
        var respuesta = (mensaje.InteractiveId ?? string.Empty).ToUpperInvariant();

        if (respuesta == "TEL_SI")
        {
            var tempData = DeserializarTempData(estado.TempData);
            await _conversationRepo.UpdateAsync(
                mensaje.PhoneNumber, FlowType.AgendarCita, StepType.Cita_Email,
                JsonSerializer.Serialize(tempData));

            await _whatsApp.SendTextAsync(mensaje.PhoneNumber,
                "Por favor ingresa tu *correo electrónico* para enviarte la confirmación:\n\n" +
                "Ejemplo: _paciente@ejemplo.com_");
            return;
        }

        if (respuesta == "TEL_CAMBIAR")
        {
            await _conversationRepo.UpdateAsync(
                mensaje.PhoneNumber, FlowType.AgendarCita, StepType.Cita_TelefonoInput,
                estado.TempData);

            await _whatsApp.SendTextAsync(mensaje.PhoneNumber,
                "Ingresa el *número de teléfono de contacto* (10 dígitos):\n\n" +
                "Ejemplo: _2216544755_");
            return;
        }

        // Re-mostrar botones si mandó texto
        var tel = DeserializarTempData(estado.TempData).PacienteTelefono ?? string.Empty;
        var fmt = tel.Length == 10 ? $"{tel[..3]}-{tel[3..6]}-{tel[6..]}" : tel;
        await _whatsApp.SendButtonsAsync(mensaje.PhoneNumber,
            $"¿Registramos la cita con el número *{fmt}*?",
        [
            ("TEL_SI", "✅ Sí, usar este"),
            ("TEL_CAMBIAR", "✏️ Usar otro número")
        ]);
    }

    // ─── Paso 2b: Ingreso manual de teléfono ─────────────────────────────────

    private async Task ProcesarTelefonoInputAsync(IncomingMessageDto mensaje, Domain.Entities.ConversationState estado)
    {
        var soloDigitos = ExtraerDiezDigitos(mensaje.TextBody.Trim());

        if (soloDigitos.Length != 10)
        {
            await _whatsApp.SendTextAsync(mensaje.PhoneNumber,
                "⚠️ El número debe tener *10 dígitos*.\n\nEjemplo: _2216544755_\n\nIntenta de nuevo:");
            return;
        }

        var tempData = DeserializarTempData(estado.TempData);
        tempData.PacienteTelefono = soloDigitos;

        await _conversationRepo.UpdateAsync(
            mensaje.PhoneNumber, FlowType.AgendarCita, StepType.Cita_Email,
            JsonSerializer.Serialize(tempData));

        await _whatsApp.SendTextAsync(mensaje.PhoneNumber,
            $"✅ Teléfono: *{soloDigitos[..3]}-{soloDigitos[3..6]}-{soloDigitos[6..]}*\n\n" +
            "Por favor ingresa tu *correo electrónico*:\n\nEjemplo: _paciente@ejemplo.com_");
    }

    // ─── Paso 3: Email ────────────────────────────────────────────────────────

    private async Task ProcesarEmailAsync(IncomingMessageDto mensaje, Domain.Entities.ConversationState estado)
    {
        var email = mensaje.TextBody.Trim().ToLowerInvariant();

        if (!_emailRegex.IsMatch(email) || email.Length > 200)
        {
            await _whatsApp.SendTextAsync(mensaje.PhoneNumber,
                "⚠️ El correo electrónico no es válido.\n\nEjemplo: _paciente@ejemplo.com_\n\nIntenta de nuevo:");
            return;
        }

        var tempData = DeserializarTempData(estado.TempData);
        tempData.PacienteEmail = email;

        await _whatsApp.SendTextAsync(mensaje.PhoneNumber,
            $"✅ Correo: *{email}*\n\n⏳ Consultando disponibilidad...");

        await DetectarYMostrarHorariosAsync(mensaje.PhoneNumber, tempData);
    }

    // ─── Auto-detección de fecha con Google Calendar ──────────────────────────

    private async Task DetectarYMostrarHorariosAsync(string phoneNumber, CitaTempData tempData)
    {
        var hoy = DateOnly.FromDateTime(DateTime.Now);
        var (fecha, slots, motivo) = await BuscarFechaConSlotsAsync(hoy, phoneNumber);

        if (!slots.Any())
        {
            var msg = motivo == "cita_existente"
                ? "📅 Ya tienes una cita agendada para hoy y mañana. Solo se permite *una cita por día*.\n\n" +
                  "Escribe *mis citas* para ver tus próximas citas o *menu* para volver al inicio."
                : "😔 No hay horarios disponibles hoy ni mañana.\n\n" +
                  "Por favor comunícate directamente al consultorio para agendar tu cita.";

            await _whatsApp.SendTextAsync(phoneNumber, msg);
            await _conversationRepo.ResetAsync(phoneNumber);
            return;
        }

        var esHoy = fecha == hoy;
        var nombreDia = ObtenerNombreDia(fecha.DayOfWeek);
        var encabezado = esHoy
            ? $"📅 Horarios disponibles para *hoy {nombreDia} {fecha:dd/MM}*:"
            : $"📅 No hay horarios disponibles hoy.\n\nHorarios disponibles para *mañana {nombreDia} {fecha:dd/MM}*:";

        tempData.FechaCita = fecha.ToString("yyyy-MM-dd");

        await _conversationRepo.UpdateAsync(
            phoneNumber, FlowType.AgendarCita, StepType.Cita_Horario,
            JsonSerializer.Serialize(tempData));

        await MostrarHorariosAsync(phoneNumber, fecha, slots.ToList(), encabezado);
    }

    private async Task<(DateOnly Fecha, IReadOnlyList<string> Slots, string? Motivo)> BuscarFechaConSlotsAsync(
        DateOnly desde, string phoneNumber)
    {
        var citasActivas = (await _citaRepo.GetByPhoneAsync(phoneNumber))
            .Where(c => c.Estado == EstadoCita.Confirmada)
            .ToList();

        for (int i = 0; i < 2; i++)
        {
            var fecha = i == 0 ? desde : SiguienteDiaHabil(desde);

            // Regla: 1 cita por WhatsApp por día
            if (citasActivas.Any(c => c.FechaCita == fecha)) continue;

            var slots = await _calendarService.GetAvailableSlotsAsync(fecha);
            if (slots.Any()) return (fecha, slots, null);
        }

        var bloquePorCita = Enumerable.Range(0, 2)
            .Select(i => i == 0 ? desde : SiguienteDiaHabil(desde))
            .All(f => citasActivas.Any(c => c.FechaCita == f));

        return (desde, Array.Empty<string>(), bloquePorCita ? "cita_existente" : null);
    }

    private static DateOnly SiguienteDiaHabil(DateOnly desde)
    {
        var sig = desde.AddDays(1);
        return sig.DayOfWeek == DayOfWeek.Sunday ? sig.AddDays(1) : sig;
    }

    // ─── Paso 4: Horario ──────────────────────────────────────────────────────

    private async Task ProcesarHorarioAsync(IncomingMessageDto mensaje, Domain.Entities.ConversationState estado)
    {
        var tempData = DeserializarTempData(estado.TempData);
        var fecha = DateOnly.Parse(tempData.FechaCita!);
        string horaTexto;

        var interactiveId = mensaje.InteractiveId ?? string.Empty;
        if (interactiveId.StartsWith("HORA_"))
        {
            var horaStr = interactiveId[5..];
            horaTexto = $"{horaStr[..2]}:{horaStr[2..]}";
        }
        else
        {
            horaTexto = mensaje.TextBody.Trim();
        }

        if (!TimeSpan.TryParse(horaTexto, out var hora))
        {
            await _whatsApp.SendTextAsync(mensaje.PhoneNumber,
                "⚠️ Hora no válida. Por favor selecciona un horario de la lista.");
            var actuales = (await _calendarService.GetAvailableSlotsAsync(fecha)).ToList();
            await MostrarHorariosAsync(mensaje.PhoneNumber, fecha, actuales);
            return;
        }

        var slotsDisponibles = await _calendarService.GetAvailableSlotsAsync(fecha);
        var horaFmt = hora.ToString(@"hh\:mm");
        if (!slotsDisponibles.Contains(horaFmt))
        {
            await _whatsApp.SendTextAsync(mensaje.PhoneNumber,
                "⚠️ Ese horario ya no está disponible. Por favor elige otro:");
            await MostrarHorariosAsync(mensaje.PhoneNumber, fecha, slotsDisponibles.ToList());
            return;
        }

        tempData.HoraCita = horaFmt;
        await _conversationRepo.UpdateAsync(
            mensaje.PhoneNumber, FlowType.AgendarCita, StepType.Cita_Motivo,
            JsonSerializer.Serialize(tempData));

        await _whatsApp.SendTextAsync(mensaje.PhoneNumber,
            "✅ Horario seleccionado.\n\n" +
            "¿Cuál es el *motivo de la consulta*? (Describe brevemente tus síntomas o motivo de visita):");
    }

    // ─── Paso 5: Motivo ───────────────────────────────────────────────────────

    private async Task ProcesarMotivoAsync(IncomingMessageDto mensaje, Domain.Entities.ConversationState estado)
    {
        var motivo = mensaje.TextBody.Trim();

        if (motivo.Length < 5)
        {
            await _whatsApp.SendTextAsync(mensaje.PhoneNumber,
                "Por favor describe brevemente el motivo de tu consulta (mínimo 5 caracteres):");
            return;
        }

        var tempData = DeserializarTempData(estado.TempData);
        tempData.Motivo = motivo;

        await _conversationRepo.UpdateAsync(
            mensaje.PhoneNumber, FlowType.AgendarCita, StepType.Cita_Confirmacion,
            JsonSerializer.Serialize(tempData));

        var fecha = DateOnly.Parse(tempData.FechaCita!);
        var resumen =
            "📋 *Resumen de tu cita:*\n\n" +
            $"👤 Paciente: {tempData.PacienteNombre}\n" +
            $"📞 Teléfono: {tempData.PacienteTelefono}\n" +
            $"📧 Email: {tempData.PacienteEmail}\n" +
            $"📅 Fecha: {fecha:dd/MM/yyyy} ({ObtenerNombreDia(fecha.DayOfWeek)})\n" +
            $"🕐 Hora: {tempData.HoraCita}\n" +
            $"📝 Motivo: {tempData.Motivo}\n\n" +
            "¿Deseas confirmar la cita?";

        await _whatsApp.SendButtonsAsync(mensaje.PhoneNumber, resumen,
        [
            ("CITA_SI", "✅ Sí, confirmar"),
            ("CITA_NO", "❌ No, cancelar")
        ]);
    }

    // ─── Paso 6: Confirmación final ───────────────────────────────────────────

    private async Task ProcesarConfirmacionAsync(IncomingMessageDto mensaje, Domain.Entities.ConversationState estado)
    {
        var respuesta = (mensaje.InteractiveId ?? mensaje.TextBody).Trim().ToUpperInvariant();

        if (respuesta is "CITA_NO" or "NO")
        {
            await _conversationRepo.ResetAsync(mensaje.PhoneNumber);
            await _whatsApp.SendTextAsync(mensaje.PhoneNumber,
                "❌ Cita cancelada. Escribe *menu* si deseas intentarlo de nuevo.");
            return;
        }

        if (respuesta is not ("CITA_SI" or "SI" or "SÍ" or "S"))
        {
            await _whatsApp.SendButtonsAsync(mensaje.PhoneNumber,
                "Por favor confirma si deseas agendar la cita:",
            [
                ("CITA_SI", "✅ Sí, confirmar"),
                ("CITA_NO", "❌ No, cancelar")
            ]);
            return;
        }

        var tempData = DeserializarTempData(estado.TempData);
        var fecha = DateOnly.Parse(tempData.FechaCita!);
        var hora = TimeSpan.Parse(tempData.HoraCita!);
        var folio = await _citaRepo.GenerateFolioAsync(fecha);

        var cita = new Cita
        {
            PacienteNombre = tempData.PacienteNombre!,
            PacienteTelefono = mensaje.PhoneNumber,
            PacienteTelefonoContacto = tempData.PacienteTelefono,
            PacienteEmail = tempData.PacienteEmail,
            FechaCita = fecha,
            HoraCita = hora,
            Motivo = tempData.Motivo!,
            Estado = EstadoCita.Confirmada,
            FolioConfirmacion = folio,
            CreadoEn = DateTime.UtcNow
        };

        await _citaRepo.CreateAsync(cita);
        await _conversationRepo.ResetAsync(mensaje.PhoneNumber);

        _logger.LogInformation("Cita agendada: {Folio} para {Phone} el {Fecha} a las {Hora}",
            folio, mensaje.PhoneNumber, fecha, hora);

        // Crear evento en Google Calendar (best-effort)
        await _calendarService.CreateEventAsync(cita);

        await _whatsApp.SendTextAsync(mensaje.PhoneNumber,
            $"✅ *¡Cita confirmada!*\n\n" +
            $"📋 *Folio:* {folio}\n" +
            $"👤 Paciente: {cita.PacienteNombre}\n" +
            $"📞 Teléfono: {cita.PacienteTelefonoContacto}\n" +
            $"📧 Email: {cita.PacienteEmail}\n" +
            $"📅 Fecha: {fecha:dd/MM/yyyy} ({ObtenerNombreDia(fecha.DayOfWeek)})\n" +
            $"🕐 Hora: {tempData.HoraCita}\n" +
            $"📝 Motivo: {cita.Motivo}\n\n" +
            "Guarda tu folio. Recibirás un recordatorio 24 horas antes de tu cita.\n\n" +
            "Escribe *menu* para regresar al menú principal.");
    }

    // ─── Consultar citas ──────────────────────────────────────────────────────

    public async Task HandleConsultaAsync(IncomingMessageDto mensaje, Domain.Entities.ConversationState estado)
    {
        await MostrarCitasAsync(mensaje.PhoneNumber);
        await _conversationRepo.ResetAsync(mensaje.PhoneNumber);
    }

    public async Task MostrarCitasAsync(string phoneNumber)
    {
        var citas = (await _citaRepo.GetByPhoneAsync(phoneNumber))
            .Where(c => c.Estado == EstadoCita.Confirmada && c.FechaCita >= DateOnly.FromDateTime(DateTime.Today))
            .OrderBy(c => c.FechaCita)
            .ThenBy(c => c.HoraCita)
            .ToList();

        if (!citas.Any())
        {
            await _whatsApp.SendTextAsync(phoneNumber,
                "📋 No tienes citas pendientes.\n\nEscribe *menu* para ver las opciones disponibles.");
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("📋 *Tus próximas citas:*\n");
        foreach (var cita in citas)
        {
            sb.AppendLine($"🗓️ *{cita.FechaCita:dd/MM/yyyy}* - {cita.HoraCita:hh\\:mm}");
            sb.AppendLine($"   Folio: {cita.FolioConfirmacion}");
            sb.AppendLine($"   Motivo: {cita.Motivo}");
            sb.AppendLine();
        }
        sb.AppendLine("Escribe *menu* para regresar al menú principal.");
        await _whatsApp.SendTextAsync(phoneNumber, sb.ToString());
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private async Task MostrarHorariosAsync(string phoneNumber, DateOnly fecha, List<string> horarios, string? encabezado = null)
    {
        var opciones = horarios.Select(h => ($"HORA_{h.Replace(":", "")}", h, "Disponible")).ToList();
        var titulo = encabezado ?? $"🕐 Selecciona un horario para el *{fecha:dd/MM/yyyy}*:";

        if (opciones.Count <= 3)
            await _whatsApp.SendButtonsAsync(phoneNumber, titulo, opciones.Select(o => (o.Item1, o.Item2)));
        else
            await _whatsApp.SendListAsync(phoneNumber, titulo, "Ver horarios", "Horarios disponibles", opciones);
    }

    private static string ExtraerDiezDigitos(string input)
    {
        var d = Regex.Replace(input, @"\D", "");
        if (d.Length == 13 && d.StartsWith("521")) d = d[3..];
        else if (d.Length == 12 && d.StartsWith("52")) d = d[2..];
        return d;
    }

    private static CitaTempData DeserializarTempData(string? json)
    {
        if (string.IsNullOrEmpty(json)) return new CitaTempData();
        return JsonSerializer.Deserialize<CitaTempData>(json, _jsonOptions) ?? new CitaTempData();
    }

    private static string ObtenerNombreDia(DayOfWeek dia) => dia switch
    {
        DayOfWeek.Monday => "Lunes",
        DayOfWeek.Tuesday => "Martes",
        DayOfWeek.Wednesday => "Miércoles",
        DayOfWeek.Thursday => "Jueves",
        DayOfWeek.Friday => "Viernes",
        DayOfWeek.Saturday => "Sábado",
        DayOfWeek.Sunday => "Domingo",
        _ => string.Empty
    };
}
