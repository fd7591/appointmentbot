using botFacturacion.Application.DTOs;
using botFacturacion.Application.Interfaces;
using botFacturacion.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace botFacturacion.Application.Services;

/// <summary>
/// Servicio principal del bot. Recibe cada mensaje entrante, gestiona la máquina
/// de estados de la conversación y delega el procesamiento al servicio correspondiente.
/// </summary>
public class ConversationService
{
    private readonly IConversationRepository _conversationRepo;
    private readonly IWhatsAppService _whatsApp;
    private readonly CitaService _citaService;
    private readonly FacturaService _facturaService;
    private readonly ILogger<ConversationService> _logger;
    private readonly int _sessionTimeoutMinutes;

    public ConversationService(
        IConversationRepository conversationRepo,
        IWhatsAppService whatsApp,
        CitaService citaService,
        FacturaService facturaService,
        ILogger<ConversationService> logger,
        int sessionTimeoutMinutes = 30)
    {
        _conversationRepo = conversationRepo;
        _whatsApp = whatsApp;
        _citaService = citaService;
        _facturaService = facturaService;
        _logger = logger;
        _sessionTimeoutMinutes = sessionTimeoutMinutes;
    }

    /// <summary>
    /// Punto de entrada principal. Procesa un mensaje normalizado del webhook.
    /// </summary>
    public async Task HandleMessageAsync(IncomingMessageDto mensaje)
    {
        try
        {
            _logger.LogInformation("Mensaje recibido de {Phone}: tipo={Type}, texto='{Text}', interactiveId='{Id}'",
                mensaje.PhoneNumber, mensaje.MessageType, mensaje.TextBody, mensaje.InteractiveId);

            // Obtener o crear el estado de conversación
            var estado = await _conversationRepo.GetOrCreateAsync(mensaje.PhoneNumber);

            // Verificar timeout de sesión — si expiró, reiniciar silenciosamente
            if (estado.CurrentFlow != FlowType.None &&
                estado.UpdatedAt < DateTime.UtcNow.AddMinutes(-_sessionTimeoutMinutes))
            {
                _logger.LogInformation("Sesión expirada para {Phone}. Reiniciando flujo.", mensaje.PhoneNumber);
                await _conversationRepo.ResetAsync(mensaje.PhoneNumber);
                estado = await _conversationRepo.GetOrCreateAsync(mensaje.PhoneNumber);
            }

            // Texto normalizado para comparaciones (solo text messages)
            var textoNorm = mensaje.TextBody.Trim().ToLowerInvariant();

            // ── Comandos globales (funcionan en cualquier paso) ───────────────
            if (textoNorm == "cancelar" || textoNorm == "cancel")
            {
                await _conversationRepo.ResetAsync(mensaje.PhoneNumber);
                await _whatsApp.SendTextAsync(mensaje.PhoneNumber,
                    "❌ Operación cancelada.\n\nEscribe *menu* para ver las opciones disponibles.");
                return;
            }

            if (textoNorm == "menu" || textoNorm == "menú" || textoNorm == "inicio" || textoNorm == "hola")
            {
                await _conversationRepo.ResetAsync(mensaje.PhoneNumber);
                await EnviarMenuPrincipalAsync(mensaje.PhoneNumber, mensaje.ProfileName);
                return;
            }

            // ── Sin flujo activo: mostrar menú o interpretar selección ────────
            if (estado.CurrentFlow == FlowType.None)
            {
                await ProcesarSinFlujoAsync(mensaje, textoNorm);
                return;
            }

            // ── Delegar al servicio del flujo activo ──────────────────────────
            switch (estado.CurrentFlow)
            {
                case FlowType.AgendarCita:
                    await _citaService.HandleStepAsync(mensaje, estado);
                    break;

                case FlowType.SolicitarFactura:
                    await _facturaService.HandleStepAsync(mensaje, estado);
                    break;

                case FlowType.ConsultarCitas:
                    await _citaService.HandleConsultaAsync(mensaje, estado);
                    break;

                default:
                    await _conversationRepo.ResetAsync(mensaje.PhoneNumber);
                    await EnviarMenuPrincipalAsync(mensaje.PhoneNumber, mensaje.ProfileName);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al procesar mensaje de {Phone}", mensaje.PhoneNumber);

            // El bot siempre responde, incluso ante errores
            try
            {
                await _whatsApp.SendTextAsync(mensaje.PhoneNumber,
                    "⚠️ Ocurrió un problema al procesar tu solicitud. Por favor intenta de nuevo o escribe *menu* para reiniciar.");
            }
            catch (Exception sendEx)
            {
                _logger.LogError(sendEx, "Error al enviar mensaje de error a {Phone}", mensaje.PhoneNumber);
            }
        }
    }

    // ─── Menú principal ────────────────────────────────────────────────────────

    /// <summary>
    /// Envía el menú principal con botones de acceso rápido.
    /// WhatsApp solo permite 3 botones por mensaje; el resto se incluye como texto.
    /// </summary>
    public async Task EnviarMenuPrincipalAsync(string phoneNumber, string? nombrePerfil = null)
    {
        var saludo = !string.IsNullOrEmpty(nombrePerfil)
            ? $"Hola, *{nombrePerfil}*! 👋\n\n"
            : "Hola! 👋\n\n";

        var cuerpo = saludo +
            "Bienvenido al bot del consultorio. ¿En qué te puedo ayudar?\n\n" +
            "📋 Elige una opción:";

        // Enviamos 3 botones principales (límite de WhatsApp)
        await _whatsApp.SendButtonsAsync(phoneNumber, cuerpo,
        [
            ("MENU_CITA",    "📅 Agendar cita"),
            ("MENU_FACTURA", "🧾 Solicitar factura"),
            ("MENU_CONSUL",  "📋 Mis citas")
        ]);

        // Mencionamos la opción de Ayuda por texto (no cabe como 4to botón)
        await _whatsApp.SendTextAsync(phoneNumber,
            "Escribe *ayuda* para obtener más información o *cancelar* en cualquier momento para reiniciar.");
    }

    // ─── Procesamiento sin flujo activo ───────────────────────────────────────

    private async Task ProcesarSinFlujoAsync(IncomingMessageDto mensaje, string textoNorm)
    {
        // Interpretar respuesta interactiva (selección del menú)
        var opcionId = mensaje.InteractiveId?.ToUpperInvariant() ?? string.Empty;

        switch (opcionId)
        {
            case "MENU_CITA":
                await IniciarFlujoCitaAsync(mensaje.PhoneNumber);
                return;

            case "MENU_FACTURA":
                await IniciarFlujoFacturaAsync(mensaje.PhoneNumber);
                return;

            case "MENU_CONSUL":
                await IniciarFlujoConsultaAsync(mensaje.PhoneNumber);
                return;
        }

        // Interpretar texto libre
        switch (textoNorm)
        {
            case "1":
            case "agendar":
            case "agendar cita":
            case "cita":
                await IniciarFlujoCitaAsync(mensaje.PhoneNumber);
                return;

            case "2":
            case "factura":
            case "solicitar factura":
            case "facturar":
                await IniciarFlujoFacturaAsync(mensaje.PhoneNumber);
                return;

            case "3":
            case "mis citas":
            case "consultar":
            case "citas":
                await IniciarFlujoConsultaAsync(mensaje.PhoneNumber);
                return;

            case "ayuda":
            case "help":
            case "4":
                await EnviarAyudaAsync(mensaje.PhoneNumber);
                return;

            default:
                // Cualquier otro texto: mostrar menú
                await EnviarMenuPrincipalAsync(mensaje.PhoneNumber, mensaje.ProfileName);
                return;
        }
    }

    // ─── Inicio de flujos ─────────────────────────────────────────────────────

    private async Task IniciarFlujoCitaAsync(string phoneNumber)
    {
        await _citaService.IniciarFlujoAsync(phoneNumber);
    }

    private async Task IniciarFlujoFacturaAsync(string phoneNumber)
    {
        await _conversationRepo.UpdateAsync(phoneNumber, FlowType.SolicitarFactura, StepType.Factura_RFC);
        await _whatsApp.SendTextAsync(phoneNumber,
            "🧾 *Solicitar Factura*\n\n" +
            "Vamos a generar tu solicitud de factura. Puedes escribir *cancelar* en cualquier momento.\n\n" +
            "Por favor, escribe tu *RFC* (Registro Federal de Contribuyentes):");
    }

    private async Task IniciarFlujoConsultaAsync(string phoneNumber)
    {
        await _conversationRepo.UpdateAsync(phoneNumber, FlowType.ConsultarCitas, StepType.ConsultarCitas_Listado);
        await _citaService.MostrarCitasAsync(phoneNumber);
        await _conversationRepo.ResetAsync(phoneNumber);
    }

    private async Task EnviarAyudaAsync(string phoneNumber)
    {
        await _whatsApp.SendTextAsync(phoneNumber,
            "❓ *Ayuda*\n\n" +
            "*Opciones disponibles:*\n" +
            "• *Agendar cita* — Programa una consulta médica\n" +
            "• *Solicitar factura* — Solicita la factura de tu consulta\n" +
            "• *Mis citas* — Consulta tus citas pendientes\n\n" +
            "*Comandos útiles:*\n" +
            "• Escribe *menu* para volver al menú principal\n" +
            "• Escribe *cancelar* para cancelar la operación actual\n\n" +
            "📞 Para emergencias, llame directamente al consultorio.");
    }
}
