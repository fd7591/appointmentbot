using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using botFacturacion.Application.DTOs;
using botFacturacion.Application.Interfaces;
using botFacturacion.Domain.Entities;
using botFacturacion.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace botFacturacion.Application.Services;

/// <summary>
/// Servicio que gestiona el flujo conversacional completo para solicitar una factura.
/// Cubre validación de RFC, captura de datos fiscales (manual o vía Constancia de Situación
/// Fiscal), datos de la consulta, confirmación y registro en BD + Google Sheets.
/// </summary>
public class FacturaService
{
    private readonly IConversationRepository _conversationRepo;
    private readonly IFacturaRepository _facturaRepo;
    private readonly IGoogleSheetsService _sheetsService;
    private readonly IWhatsAppService _whatsApp;
    private readonly IAzureDocumentIntelligenceService _azureService;
    private readonly bool _facturacionSoloMesActual;
    private readonly ILogger<FacturaService> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    // Regex RFC según SAT México
    // Persona física: 4 letras + 6 números (AAMMDD) + 3 caracteres homoclave
    // Persona moral: 3 letras + 6 números (AAMMDD) + 3 caracteres homoclave
    private static readonly Regex _rfcRegex = new(
        @"^([A-ZÑ&]{3,4})(\d{2})(0[1-9]|1[0-2])(0[1-9]|[12]\d|3[01])([A-Z\d]{3})$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex _emailRegex = new(
        @"^[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}$",
        RegexOptions.Compiled);

    private static readonly Regex _codigoPostalRegex = new(@"^\d{5}$", RegexOptions.Compiled);

    public FacturaService(
        IConversationRepository conversationRepo,
        IFacturaRepository facturaRepo,
        IGoogleSheetsService sheetsService,
        IWhatsAppService whatsApp,
        IAzureDocumentIntelligenceService azureService,
        bool facturacionSoloMesActual,
        ILogger<FacturaService> logger)
    {
        _conversationRepo = conversationRepo;
        _facturaRepo = facturaRepo;
        _sheetsService = sheetsService;
        _whatsApp = whatsApp;
        _azureService = azureService;
        _facturacionSoloMesActual = facturacionSoloMesActual;
        _logger = logger;
    }

    // ─── Máquina de estados del flujo Solicitar Factura ──────────────────────

    public async Task HandleStepAsync(IncomingMessageDto mensaje, Domain.Entities.ConversationState estado)
    {
        try
        {
            switch (estado.CurrentStep)
            {
                case StepType.Factura_RFC:
                    await ProcesarRFCAsync(mensaje, estado);
                    break;

                case StepType.Factura_ConfirmacionDatosFiscales:
                    await ProcesarConfirmacionDatosFiscalesAsync(mensaje, estado);
                    break;

                case StepType.Factura_RazonSocial:
                    await ProcesarRazonSocialAsync(mensaje, estado);
                    break;

                case StepType.Factura_RegimenFiscal:
                    await ProcesarRegimenFiscalAsync(mensaje, estado);
                    break;

                case StepType.Factura_CodigoPostal:
                    await ProcesarCodigoPostalAsync(mensaje, estado);
                    break;

                case StepType.Factura_Email:
                    await ProcesarEmailAsync(mensaje, estado);
                    break;

                case StepType.Factura_FechaConsulta:
                    await ProcesarFechaConsultaAsync(mensaje, estado);
                    break;

                case StepType.Factura_Monto:
                    await ProcesarMontoAsync(mensaje, estado);
                    break;

                case StepType.Factura_MetodoPago:
                    await ProcesarMetodoPagoAsync(mensaje, estado);
                    break;

                case StepType.Factura_FormaPago:
                    await ProcesarFormaPagoAsync(mensaje, estado);
                    break;

                case StepType.Factura_UsoCFDI:
                    await ProcesarUsoCFDIAsync(mensaje, estado);
                    break;

                case StepType.Factura_ConfirmacionFinal:
                    await ProcesarConfirmacionFinalAsync(mensaje, estado);
                    break;

                case StepType.Factura_OpcionIngresoDatos:
                    await ProcesarOpcionIngresoDatosAsync(mensaje, estado);
                    break;

                case StepType.Factura_EsperandoConstancia:
                    await ProcesarConstanciaAsync(mensaje, estado);
                    break;

                default:
                    _logger.LogWarning("Paso desconocido {Step} en flujo Factura para {Phone}",
                        estado.CurrentStep, mensaje.PhoneNumber);
                    await _conversationRepo.ResetAsync(mensaje.PhoneNumber);
                    await _whatsApp.SendTextAsync(mensaje.PhoneNumber,
                        "⚠️ Ocurrió un error en el flujo. Escribe *menu* para reiniciar.");
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en flujo Factura paso {Step} para {Phone}",
                estado.CurrentStep, mensaje.PhoneNumber);
            throw;
        }
    }

    // ─── Paso 1: RFC ──────────────────────────────────────────────────────────

    private async Task ProcesarRFCAsync(IncomingMessageDto mensaje, Domain.Entities.ConversationState estado)
    {
        var rfc = mensaje.TextBody.Trim().ToUpperInvariant();

        // Validar formato RFC
        if (!_rfcRegex.IsMatch(rfc))
        {
            await _whatsApp.SendTextAsync(mensaje.PhoneNumber,
                "⚠️ El RFC ingresado no tiene un formato válido.\n\n" +
                "• Persona física: 13 caracteres (ej. GOMJ850312AB3)\n" +
                "• Persona moral: 12 caracteres (ej. ABC850312AB3)\n\n" +
                "Por favor, ingresa tu RFC:");
            return;
        }

        // Buscar si ya existe en base de datos
        var receptorExistente = await _facturaRepo.GetReceptorByRFCAsync(rfc);

        var tempData = new FacturaTempData { RFC = rfc };

        if (receptorExistente != null)
        {
            // RFC encontrado — mostrar datos guardados y preguntar si desea usarlos
            tempData.ReceptorFiscalId = receptorExistente.Id;
            tempData.RazonSocial = receptorExistente.RazonSocial;
            tempData.RegimenFiscalClave = receptorExistente.RegimenFiscalClave;
            tempData.RegimenFiscalDescripcion = receptorExistente.RegimenFiscalDescripcion;
            tempData.CodigoPostal = receptorExistente.CodigoPostal;
            tempData.Email = receptorExistente.Email;

            await _conversationRepo.UpdateAsync(
                mensaje.PhoneNumber,
                FlowType.SolicitarFactura,
                StepType.Factura_ConfirmacionDatosFiscales,
                JsonSerializer.Serialize(tempData));

            var resumenDatos =
                $"✅ RFC encontrado en nuestros registros:\n\n" +
                $"🏢 *{receptorExistente.RazonSocial}*\n" +
                $"📋 Régimen: {receptorExistente.RegimenFiscalClave} - {receptorExistente.RegimenFiscalDescripcion}\n" +
                $"📮 C.P.: {receptorExistente.CodigoPostal}\n" +
                $"📧 Email: {receptorExistente.Email}\n\n" +
                "¿Deseas usar estos datos fiscales?";

            await _whatsApp.SendButtonsAsync(mensaje.PhoneNumber, resumenDatos,
            [
                ("DATOSFISCALES_SI", "✅ Sí, usar estos"),
                ("DATOSFISCALES_NO", "✏️ Actualizar datos")
            ]);
        }
        else
        {
            // RFC nuevo — ofrecer captura manual o lectura de Constancia de Situación Fiscal
            await _conversationRepo.UpdateAsync(
                mensaje.PhoneNumber,
                FlowType.SolicitarFactura,
                StepType.Factura_OpcionIngresoDatos,
                JsonSerializer.Serialize(tempData));

            await _whatsApp.SendButtonsAsync(mensaje.PhoneNumber,
                $"📝 RFC *{rfc}* no registrado. ¿Cómo deseas ingresar tus datos fiscales?",
            [
                ("OPCION_CONSTANCIA", "📄 Constancia fiscal"),
                ("OPCION_MANUAL", "✏️ Captura manual")
            ]);
        }
    }

    // ─── Paso 2A: Confirmación de datos fiscales guardados ───────────────────

    private async Task ProcesarConfirmacionDatosFiscalesAsync(IncomingMessageDto mensaje, Domain.Entities.ConversationState estado)
    {
        var respuesta = (mensaje.InteractiveId ?? mensaje.TextBody).Trim().ToUpperInvariant();

        if (respuesta is "DATOSFISCALES_SI" or "SI" or "SÍ" or "S")
        {
            // Usar datos guardados — saltar al paso de datos de consulta
            var tempData = DeserializarTempData(estado.TempData);
            tempData.UsarDatosGuardados = true;

            await _conversationRepo.UpdateAsync(
                mensaje.PhoneNumber,
                FlowType.SolicitarFactura,
                StepType.Factura_FechaConsulta,
                JsonSerializer.Serialize(tempData));

            await _whatsApp.SendTextAsync(mensaje.PhoneNumber,
                "✅ Usaremos tus datos fiscales guardados.\n\n" +
                "Ahora necesitamos los datos de la consulta.\n\n" +
                "¿Cuál fue la *fecha de la consulta* que deseas facturar? (Formato: DD/MM/YYYY)");
        }
        else if (respuesta is "DATOSFISCALES_NO" or "NO" or "N")
        {
            // Actualizar datos — ir al formulario completo
            var tempData = DeserializarTempData(estado.TempData);
            tempData.UsarDatosGuardados = false;

            await _conversationRepo.UpdateAsync(
                mensaje.PhoneNumber,
                FlowType.SolicitarFactura,
                StepType.Factura_RazonSocial,
                JsonSerializer.Serialize(tempData));

            await _whatsApp.SendTextAsync(mensaje.PhoneNumber,
                "✏️ Actualicemos tus datos fiscales.\n\n" +
                "¿Cuál es tu *razón social o nombre completo* tal como aparece en el SAT?");
        }
        else
        {
            await _whatsApp.SendButtonsAsync(mensaje.PhoneNumber,
                "Por favor confirma si deseas usar los datos fiscales guardados:",
            [
                ("DATOSFISCALES_SI", "✅ Sí, usar estos"),
                ("DATOSFISCALES_NO", "✏️ Actualizar datos")
            ]);
        }
    }

    // ─── Paso 2B: Razón social ────────────────────────────────────────────────

    private async Task ProcesarRazonSocialAsync(IncomingMessageDto mensaje, Domain.Entities.ConversationState estado)
    {
        var razonSocial = mensaje.TextBody.Trim();

        if (razonSocial.Length < 3 || razonSocial.Length > 200)
        {
            await _whatsApp.SendTextAsync(mensaje.PhoneNumber,
                "⚠️ La razón social debe tener entre 3 y 200 caracteres.\n\n" +
                "¿Cuál es tu *razón social o nombre completo* tal como aparece en el SAT?");
            return;
        }

        var tempData = DeserializarTempData(estado.TempData);
        tempData.RazonSocial = razonSocial.ToUpperInvariant(); // SAT usa mayúsculas

        await _conversationRepo.UpdateAsync(
            mensaje.PhoneNumber,
            FlowType.SolicitarFactura,
            StepType.Factura_RegimenFiscal,
            JsonSerializer.Serialize(tempData));

        await MostrarOpcionesRegimenFiscalAsync(mensaje.PhoneNumber, tempData.RFC!);
    }

    private async Task MostrarOpcionesRegimenFiscalAsync(string phoneNumber, string rfc)
    {
        // Determinar si es persona física (13 chars) o moral (12 chars) para mostrar regímenes relevantes
        var esPersonaFisica = rfc.Length == 13;

        var regimenes = esPersonaFisica
            ? RegimenFiscalExtensions.CommonesFisica
            : RegimenFiscalExtensions.CommonesMoral;

        var opciones = regimenes
            .Select(r => ($"REGIMEN_{r.GetClave()}", $"{r.GetClave()} - {r.GetDescripcion()[..Math.Min(r.GetDescripcion().Length, 60)]}", ""))
            .ToList();

        await _whatsApp.SendListAsync(
            phoneNumber,
            "¿Cuál es tu *régimen fiscal* ante el SAT?",
            "Ver regímenes",
            esPersonaFisica ? "Personas Físicas" : "Personas Morales",
            opciones!);
    }

    // ─── Paso 2C: Régimen fiscal ──────────────────────────────────────────────

    private async Task ProcesarRegimenFiscalAsync(IncomingMessageDto mensaje, Domain.Entities.ConversationState estado)
    {
        var tempData = DeserializarTempData(estado.TempData);
        string clave;

        var interactiveId = mensaje.InteractiveId ?? string.Empty;
        if (interactiveId.StartsWith("REGIMEN_"))
            clave = interactiveId[8..];
        else
            clave = mensaje.TextBody.Trim();

        var regimen = RegimenFiscalExtensions.FromClave(clave);
        if (regimen == null)
        {
            await _whatsApp.SendTextAsync(mensaje.PhoneNumber,
                "⚠️ Régimen fiscal no reconocido. Por favor selecciona una opción de la lista:");
            await MostrarOpcionesRegimenFiscalAsync(mensaje.PhoneNumber, tempData.RFC!);
            return;
        }

        tempData.RegimenFiscalClave = regimen.Value.GetClave();
        tempData.RegimenFiscalDescripcion = regimen.Value.GetDescripcion();

        await _conversationRepo.UpdateAsync(
            mensaje.PhoneNumber,
            FlowType.SolicitarFactura,
            StepType.Factura_CodigoPostal,
            JsonSerializer.Serialize(tempData));

        await _whatsApp.SendTextAsync(mensaje.PhoneNumber,
            $"✅ Régimen seleccionado: *{tempData.RegimenFiscalClave} - {tempData.RegimenFiscalDescripcion}*\n\n" +
            "¿Cuál es tu *código postal* del domicilio fiscal (5 dígitos)?");
    }

    // ─── Paso 2D: Código postal ────────────────────────────────────────────────

    private async Task ProcesarCodigoPostalAsync(IncomingMessageDto mensaje, Domain.Entities.ConversationState estado)
    {
        var cp = mensaje.TextBody.Trim();

        if (!_codigoPostalRegex.IsMatch(cp))
        {
            await _whatsApp.SendTextAsync(mensaje.PhoneNumber,
                "⚠️ El código postal debe ser de 5 dígitos (ej. 06600).\n\n" +
                "¿Cuál es tu *código postal* del domicilio fiscal?");
            return;
        }

        var tempData = DeserializarTempData(estado.TempData);
        tempData.CodigoPostal = cp;

        await _conversationRepo.UpdateAsync(
            mensaje.PhoneNumber,
            FlowType.SolicitarFactura,
            StepType.Factura_Email,
            JsonSerializer.Serialize(tempData));

        await _whatsApp.SendTextAsync(mensaje.PhoneNumber,
            "¿Cuál es tu *correo electrónico* para el envío del CFDI?\n\n" +
            "(Ej: tunombre@correo.com)");
    }

    // ─── Paso 2E: Email ───────────────────────────────────────────────────────

    private async Task ProcesarEmailAsync(IncomingMessageDto mensaje, Domain.Entities.ConversationState estado)
    {
        var email = mensaje.TextBody.Trim().ToLowerInvariant();

        if (!_emailRegex.IsMatch(email))
        {
            await _whatsApp.SendTextAsync(mensaje.PhoneNumber,
                "⚠️ El correo electrónico no tiene un formato válido.\n\n" +
                "¿Cuál es tu *correo electrónico* para el envío del CFDI?");
            return;
        }

        var tempData = DeserializarTempData(estado.TempData);
        tempData.Email = email;

        await _conversationRepo.UpdateAsync(
            mensaje.PhoneNumber,
            FlowType.SolicitarFactura,
            StepType.Factura_FechaConsulta,
            JsonSerializer.Serialize(tempData));

        await _whatsApp.SendTextAsync(mensaje.PhoneNumber,
            "✅ Datos fiscales registrados.\n\n" +
            "Ahora necesitamos los datos de la consulta.\n\n" +
            "¿Cuál fue la *fecha de la consulta* a facturar? (Formato: DD/MM/YYYY)");
    }

    // ─── Paso 3A: Fecha de la consulta ───────────────────────────────────────

    private async Task ProcesarFechaConsultaAsync(IncomingMessageDto mensaje, Domain.Entities.ConversationState estado)
    {
        var textoFecha = mensaje.TextBody.Trim();

        if (!DateOnly.TryParseExact(textoFecha, "dd/MM/yyyy", out var fecha))
        {
            await _whatsApp.SendTextAsync(mensaje.PhoneNumber,
                "⚠️ Formato de fecha no válido. Usa el formato *DD/MM/YYYY* (ej. 15/05/2025):\n\n" +
                "¿Cuál fue la *fecha de la consulta*?");
            return;
        }

        var hoy = DateOnly.FromDateTime(DateTime.Today);

        // La fecha no puede ser futura
        if (fecha > hoy)
        {
            await _whatsApp.SendTextAsync(mensaje.PhoneNumber,
                "⚠️ La fecha de la consulta no puede ser una fecha futura.\n\n" +
                "¿Cuál fue la *fecha de la consulta*? (DD/MM/YYYY)");
            return;
        }

        if (_facturacionSoloMesActual)
        {
            // Regla de negocio: solo se pueden facturar consultas del mes calendario actual
            var primerDiaMes = new DateOnly(hoy.Year, hoy.Month, 1);
            if (fecha < primerDiaMes)
            {
                await _whatsApp.SendTextAsync(mensaje.PhoneNumber,
                    $"⚠️ Solo se pueden facturar consultas realizadas en el mes actual " +
                    $"(*{hoy:MMMM yyyy}*).\n\n" +
                    "Si tu consulta fue en un mes anterior, comunícate directamente con el consultorio.\n\n" +
                    "¿Cuál fue la *fecha de la consulta*? (DD/MM/YYYY)");
                return;
            }
        }
        else
        {
            // Regla general SAT: no más de 2 años atrás
            if (fecha < DateOnly.FromDateTime(DateTime.Today.AddYears(-2)))
            {
                await _whatsApp.SendTextAsync(mensaje.PhoneNumber,
                    "⚠️ La fecha de la consulta es demasiado antigua (más de 2 años).\n\n" +
                    "¿Cuál fue la *fecha de la consulta*? (DD/MM/YYYY)");
                return;
            }
        }

        var tempData = DeserializarTempData(estado.TempData);
        tempData.FechaConsulta = fecha.ToString("yyyy-MM-dd");

        await _conversationRepo.UpdateAsync(
            mensaje.PhoneNumber,
            FlowType.SolicitarFactura,
            StepType.Factura_Monto,
            JsonSerializer.Serialize(tempData));

        await _whatsApp.SendTextAsync(mensaje.PhoneNumber,
            "¿Cuánto pagaste por la consulta? Ingresa el *monto* en pesos (solo números, sin comas ni signos):\n\n" +
            "Ejemplo: *850* o *1200.50*");
    }

    // ─── Paso 3B: Monto ───────────────────────────────────────────────────────

    private async Task ProcesarMontoAsync(IncomingMessageDto mensaje, Domain.Entities.ConversationState estado)
    {
        var textoMonto = mensaje.TextBody.Trim().Replace(",", "");

        if (!decimal.TryParse(textoMonto, out var monto) || monto <= 0 || monto > 999999)
        {
            await _whatsApp.SendTextAsync(mensaje.PhoneNumber,
                "⚠️ Monto no válido. Ingresa solo números positivos menores a 999,999.\n\n" +
                "¿Cuánto fue el *monto* pagado por la consulta?");
            return;
        }

        var tempData = DeserializarTempData(estado.TempData);
        tempData.Monto = Math.Round(monto, 2);

        await _conversationRepo.UpdateAsync(
            mensaje.PhoneNumber,
            FlowType.SolicitarFactura,
            StepType.Factura_MetodoPago,
            JsonSerializer.Serialize(tempData));

        // Mostrar métodos de pago
        await _whatsApp.SendListAsync(
            mensaje.PhoneNumber,
            "¿Cuál fue el *método de pago*?",
            "Ver opciones",
            "Métodos de pago SAT",
        [
            ("MP_01", "01 - Efectivo", "Pago en efectivo"),
            ("MP_02", "02 - Cheque nominativo", "Cheque a nombre del receptor"),
            ("MP_03", "03 - Transferencia electrónica", "SPEI, transferencia bancaria"),
            ("MP_04", "04 - Tarjeta de crédito", "Cualquier tarjeta de crédito"),
            ("MP_28", "28 - Tarjeta de débito", "Cualquier tarjeta de débito")
        ]);
    }

    // ─── Paso 3C: Método de pago ──────────────────────────────────────────────

    private async Task ProcesarMetodoPagoAsync(IncomingMessageDto mensaje, Domain.Entities.ConversationState estado)
    {
        var interactiveId = mensaje.InteractiveId ?? string.Empty;
        string clave;

        if (interactiveId.StartsWith("MP_"))
            clave = interactiveId[3..];
        else
            clave = mensaje.TextBody.Trim().PadLeft(2, '0');

        var metodo = MetodoPagoExtensions.FromClave(clave);
        if (metodo == null)
        {
            await _whatsApp.SendTextAsync(mensaje.PhoneNumber,
                "⚠️ Método de pago no reconocido. Por favor selecciona una opción de la lista:");
            await _whatsApp.SendListAsync(
                mensaje.PhoneNumber, "¿Cuál fue el *método de pago*?", "Ver opciones", "Métodos de pago SAT",
            [
                ("MP_01", "01 - Efectivo", ""),
                ("MP_02", "02 - Cheque nominativo", ""),
                ("MP_03", "03 - Transferencia electrónica", ""),
                ("MP_04", "04 - Tarjeta de crédito", ""),
                ("MP_28", "28 - Tarjeta de débito", "")
            ]);
            return;
        }

        var tempData = DeserializarTempData(estado.TempData);
        tempData.MetodoPagoClave = metodo.Value.GetClave();
        tempData.MetodoPagoDescripcion = metodo.Value.GetDescripcion();

        await _conversationRepo.UpdateAsync(
            mensaje.PhoneNumber,
            FlowType.SolicitarFactura,
            StepType.Factura_FormaPago,
            JsonSerializer.Serialize(tempData));

        await _whatsApp.SendButtonsAsync(
            mensaje.PhoneNumber,
            "¿Cuál fue la *forma de pago*?",
        [
            ("FP_PUE", "PUE - Pago en una sola exhibición"),
            ("FP_PPD", "PPD - Pago en parcialidades")
        ]);
    }

    // ─── Paso 3D: Forma de pago ───────────────────────────────────────────────

    private async Task ProcesarFormaPagoAsync(IncomingMessageDto mensaje, Domain.Entities.ConversationState estado)
    {
        var respuesta = (mensaje.InteractiveId ?? mensaje.TextBody).Trim().ToUpperInvariant();

        string formaPago;
        if (respuesta is "FP_PUE" or "PUE")
            formaPago = "PUE";
        else if (respuesta is "FP_PPD" or "PPD")
            formaPago = "PPD";
        else
        {
            await _whatsApp.SendButtonsAsync(
                mensaje.PhoneNumber,
                "Por favor selecciona la *forma de pago*:",
            [
                ("FP_PUE", "PUE - Pago en una exhibición"),
                ("FP_PPD", "PPD - Pago en parcialidades")
            ]);
            return;
        }

        var tempData = DeserializarTempData(estado.TempData);
        tempData.FormaPago = formaPago;

        await _conversationRepo.UpdateAsync(
            mensaje.PhoneNumber,
            FlowType.SolicitarFactura,
            StepType.Factura_UsoCFDI,
            JsonSerializer.Serialize(tempData));

        await _whatsApp.SendListAsync(
            mensaje.PhoneNumber,
            "¿Cuál es el *uso del CFDI*?",
            "Ver opciones",
            "Usos del CFDI",
        [
            ("USO_G03", "G03 - Gastos en general", "Para gastos generales"),
            ("USO_D01", "D01 - Honorarios médicos", "Honorarios médicos, dentales y hospitalarios"),
            ("USO_S01", "S01 - Sin efectos fiscales", "Sin impacto fiscal"),
            ("USO_P01", "P01 - Por definir", "Se definirá con el contador")
        ]);
    }

    // ─── Paso 3E: Uso del CFDI ────────────────────────────────────────────────

    private async Task ProcesarUsoCFDIAsync(IncomingMessageDto mensaje, Domain.Entities.ConversationState estado)
    {
        var interactiveId = mensaje.InteractiveId ?? string.Empty;
        string clave;

        if (interactiveId.StartsWith("USO_"))
            clave = interactiveId[4..];
        else
            clave = mensaje.TextBody.Trim().ToUpperInvariant();

        var uso = UsoCFDIExtensions.FromClave(clave);
        if (uso == null)
        {
            await _whatsApp.SendTextAsync(mensaje.PhoneNumber,
                "⚠️ Uso de CFDI no reconocido. Por favor selecciona una opción de la lista:");
            await _whatsApp.SendListAsync(
                mensaje.PhoneNumber, "¿Cuál es el *uso del CFDI*?", "Ver opciones", "Usos del CFDI",
            [
                ("USO_G03", "G03 - Gastos en general", ""),
                ("USO_D01", "D01 - Honorarios médicos", ""),
                ("USO_S01", "S01 - Sin efectos fiscales", ""),
                ("USO_P01", "P01 - Por definir", "")
            ]);
            return;
        }

        var tempData = DeserializarTempData(estado.TempData);
        tempData.UsoCFDIClave = uso.Value.GetClave();
        tempData.UsoCFDIDescripcion = uso.Value.GetDescripcion();

        await _conversationRepo.UpdateAsync(
            mensaje.PhoneNumber,
            FlowType.SolicitarFactura,
            StepType.Factura_ConfirmacionFinal,
            JsonSerializer.Serialize(tempData));

        // Mostrar resumen completo
        await EnviarResumenFacturaAsync(mensaje.PhoneNumber, tempData);
    }

    // ─── Paso 4: Confirmación final ───────────────────────────────────────────

    private async Task EnviarResumenFacturaAsync(string phoneNumber, FacturaTempData data)
    {
        var fechaConsulta = DateOnly.Parse(data.FechaConsulta!);
        var sb = new StringBuilder();
        sb.AppendLine("📋 *Resumen de tu solicitud de factura:*\n");
        sb.AppendLine($"🔑 RFC: {data.RFC}");
        sb.AppendLine($"🏢 Razón social: {data.RazonSocial}");
        sb.AppendLine($"📊 Régimen fiscal: {data.RegimenFiscalClave} - {data.RegimenFiscalDescripcion}");
        sb.AppendLine($"📮 C.P.: {data.CodigoPostal}");
        sb.AppendLine($"📧 Email: {data.Email}");
        sb.AppendLine();
        sb.AppendLine($"📅 Fecha consulta: {fechaConsulta:dd/MM/yyyy}");
        sb.AppendLine($"💰 Monto: ${data.Monto:N2}");
        sb.AppendLine($"💳 Método de pago: {data.MetodoPagoClave} - {data.MetodoPagoDescripcion}");
        sb.AppendLine($"📑 Forma de pago: {data.FormaPago}");
        sb.AppendLine($"🏷️ Uso CFDI: {data.UsoCFDIClave} - {data.UsoCFDIDescripcion}");
        sb.AppendLine();
        sb.Append("¿Los datos son correctos?");

        await _whatsApp.SendButtonsAsync(phoneNumber, sb.ToString(),
        [
            ("FACT_CONFIRMAR", "✅ Confirmar"),
            ("FACT_CANCELAR", "❌ Cancelar")
        ]);
    }

    private async Task ProcesarConfirmacionFinalAsync(IncomingMessageDto mensaje, Domain.Entities.ConversationState estado)
    {
        var respuesta = (mensaje.InteractiveId ?? mensaje.TextBody).Trim().ToUpperInvariant();

        if (respuesta is "FACT_CANCELAR" or "NO" or "N")
        {
            await _conversationRepo.ResetAsync(mensaje.PhoneNumber);
            await _whatsApp.SendTextAsync(mensaje.PhoneNumber,
                "❌ Solicitud cancelada. Escribe *menu* si deseas intentarlo de nuevo.");
            return;
        }

        if (respuesta is not ("FACT_CONFIRMAR" or "SI" or "SÍ" or "S"))
        {
            var tempData2 = DeserializarTempData(estado.TempData);
            await EnviarResumenFacturaAsync(mensaje.PhoneNumber, tempData2);
            return;
        }

        // Guardar en base de datos y en Google Sheets
        var tempData = DeserializarTempData(estado.TempData);
        var folio = await _facturaRepo.GenerateFolioAsync();

        // Obtener o crear el receptor fiscal
        ReceptorFiscal receptor;
        if (tempData.UsarDatosGuardados && tempData.ReceptorFiscalId.HasValue)
        {
            receptor = (await _facturaRepo.GetReceptorByRFCAsync(tempData.RFC!))!;
        }
        else
        {
            var receptorExistente = await _facturaRepo.GetReceptorByRFCAsync(tempData.RFC!);
            if (receptorExistente != null)
            {
                // Actualizar datos existentes
                receptorExistente.RazonSocial = tempData.RazonSocial!;
                receptorExistente.RegimenFiscalClave = tempData.RegimenFiscalClave!;
                receptorExistente.RegimenFiscalDescripcion = tempData.RegimenFiscalDescripcion!;
                receptorExistente.CodigoPostal = tempData.CodigoPostal!;
                receptorExistente.Email = tempData.Email!;
                receptorExistente.ActualizadoEn = DateTime.UtcNow;
                receptor = await _facturaRepo.UpdateReceptorAsync(receptorExistente);
            }
            else
            {
                // Crear nuevo receptor
                receptor = await _facturaRepo.CreateReceptorAsync(new ReceptorFiscal
                {
                    RFC = tempData.RFC!,
                    RazonSocial = tempData.RazonSocial!,
                    RegimenFiscalClave = tempData.RegimenFiscalClave!,
                    RegimenFiscalDescripcion = tempData.RegimenFiscalDescripcion!,
                    CodigoPostal = tempData.CodigoPostal!,
                    Email = tempData.Email!,
                    TelefonoRegistro = mensaje.PhoneNumber,
                    CreadoEn = DateTime.UtcNow,
                    ActualizadoEn = DateTime.UtcNow
                });
            }
        }

        var fechaConsulta = DateOnly.Parse(tempData.FechaConsulta!);
        var solicitud = new SolicitudFactura
        {
            Folio = folio,
            TelefonoSolicitante = mensaje.PhoneNumber,
            ReceptorFiscalId = receptor.Id,
            FechaConsulta = fechaConsulta,
            Monto = tempData.Monto!.Value,
            MetodoPagoClave = tempData.MetodoPagoClave!,
            MetodoPagoDescripcion = tempData.MetodoPagoDescripcion!,
            FormaPago = tempData.FormaPago!,
            UsoCFDIClave = tempData.UsoCFDIClave!,
            UsoCFDIDescripcion = tempData.UsoCFDIDescripcion!,
            Estado = EstadoSolicitud.Pendiente,
            CreadoEn = DateTime.UtcNow,
            ActualizadoEn = DateTime.UtcNow
        };

        solicitud = await _facturaRepo.CreateSolicitudAsync(solicitud);

        // Registrar en Google Sheets
        int filaSheets = 0;
        try
        {
            var row = new GoogleSheetsRowDto
            {
                Folio = folio,
                FechaSolicitud = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"),
                TelefonoSolicitante = mensaje.PhoneNumber,
                RFC = receptor.RFC,
                RazonSocial = receptor.RazonSocial,
                RegimenFiscalClave = receptor.RegimenFiscalClave,
                RegimenFiscalDescripcion = receptor.RegimenFiscalDescripcion,
                CodigoPostal = receptor.CodigoPostal,
                Email = receptor.Email,
                FechaConsulta = fechaConsulta.ToString("dd/MM/yyyy"),
                Monto = tempData.Monto.Value.ToString("N2"),
                MetodoPagoClave = tempData.MetodoPagoClave!,
                MetodoPagoDescripcion = tempData.MetodoPagoDescripcion!,
                FormaPago = tempData.FormaPago!,
                UsoCFDIClave = tempData.UsoCFDIClave!,
                UsoCFDIDescripcion = tempData.UsoCFDIDescripcion!,
                Estado = "PENDIENTE"
            };

            filaSheets = await _sheetsService.AppendRowAsync(row);
            await _facturaRepo.UpdateFilaGoogleSheetsAsync(solicitud.Id, filaSheets);
        }
        catch (Exception ex)
        {
            // No interrumpir el flujo si Sheets falla; solo loguear
            _logger.LogError(ex, "Error al escribir en Google Sheets para folio {Folio}", folio);
        }

        await _conversationRepo.ResetAsync(mensaje.PhoneNumber);

        _logger.LogInformation("Solicitud de factura creada: {Folio} para {Phone}", folio, mensaje.PhoneNumber);

        await _whatsApp.SendTextAsync(mensaje.PhoneNumber,
            $"✅ *¡Solicitud de factura recibida!*\n\n" +
            $"📋 *Folio:* {folio}\n" +
            $"📧 Tu factura será enviada a: {receptor.Email}\n\n" +
            $"Nuestro equipo procesará tu solicitud en las próximas 24 horas hábiles.\n\n" +
            $"Escribe *menu* para regresar al menú principal.");
    }

    // ─── Paso 2F: Opción de ingreso de datos (manual vs Constancia SAT) ──────

    private async Task ProcesarOpcionIngresoDatosAsync(IncomingMessageDto mensaje, Domain.Entities.ConversationState estado)
    {
        var respuesta = (mensaje.InteractiveId ?? mensaje.TextBody).Trim().ToUpperInvariant();
        var tempData = DeserializarTempData(estado.TempData);

        if (respuesta is "OPCION_CONSTANCIA")
        {
            await _conversationRepo.UpdateAsync(
                mensaje.PhoneNumber,
                FlowType.SolicitarFactura,
                StepType.Factura_EsperandoConstancia,
                JsonSerializer.Serialize(tempData));

            await _whatsApp.SendTextAsync(mensaje.PhoneNumber,
                "📄 Envía tu *Constancia de Situación Fiscal* como archivo PDF o foto.\n\n" +
                "Puedes descargarla en *sat.gob.mx* → Mi Portal SAT → Trámites → Constancia de situación fiscal.\n\n" +
                "_Extraeremos RFC, razón social, régimen fiscal y código postal automáticamente._");
        }
        else if (respuesta is "OPCION_MANUAL")
        {
            await _conversationRepo.UpdateAsync(
                mensaje.PhoneNumber,
                FlowType.SolicitarFactura,
                StepType.Factura_RazonSocial,
                JsonSerializer.Serialize(tempData));

            await _whatsApp.SendTextAsync(mensaje.PhoneNumber,
                "✏️ Ingresa los datos manualmente.\n\n" +
                "¿Cuál es tu *razón social o nombre completo* tal como aparece en el SAT?");
        }
        else
        {
            await _whatsApp.SendButtonsAsync(mensaje.PhoneNumber,
                $"¿Cómo deseas ingresar los datos fiscales para el RFC *{tempData.RFC}*?",
            [
                ("OPCION_CONSTANCIA", "📄 Constancia fiscal"),
                ("OPCION_MANUAL", "✏️ Captura manual")
            ]);
        }
    }

    // ─── Paso 2G: Recepción y análisis de la Constancia de Situación Fiscal ──

    private async Task ProcesarConstanciaAsync(IncomingMessageDto mensaje, Domain.Entities.ConversationState estado)
    {
        var tempData = DeserializarTempData(estado.TempData);

        // ── Si ya procesamos la constancia, estamos esperando confirmación ──
        if (tempData.ConstanciaProcesada)
        {
            var respuesta = (mensaje.InteractiveId ?? mensaje.TextBody).Trim().ToUpperInvariant();

            if (respuesta is "CONST_CONFIRMAR")
            {
                await _conversationRepo.UpdateAsync(
                    mensaje.PhoneNumber,
                    FlowType.SolicitarFactura,
                    StepType.Factura_Email,
                    JsonSerializer.Serialize(tempData));

                await _whatsApp.SendTextAsync(mensaje.PhoneNumber,
                    "✅ Datos fiscales confirmados.\n\n" +
                    "¿Cuál es tu *correo electrónico* para el envío del CFDI?\n\n" +
                    "(Ej: tunombre@correo.com)");
            }
            else if (respuesta is "CONST_EDITAR")
            {
                // Pre-llenar con lo extraído para que el usuario solo corrija
                tempData.ConstanciaProcesada = false;

                await _conversationRepo.UpdateAsync(
                    mensaje.PhoneNumber,
                    FlowType.SolicitarFactura,
                    StepType.Factura_RazonSocial,
                    JsonSerializer.Serialize(tempData));

                var textoActual = !string.IsNullOrEmpty(tempData.RazonSocial)
                    ? $"Valor actual: _{tempData.RazonSocial}_\n\n"
                    : string.Empty;

                await _whatsApp.SendTextAsync(mensaje.PhoneNumber,
                    $"✏️ Corrijamos los datos manualmente.\n\n{textoActual}" +
                    "¿Cuál es tu *razón social o nombre completo* tal como aparece en el SAT?");
            }
            else
            {
                await MostrarResumenConstanciaAsync(mensaje.PhoneNumber, tempData);
            }
            return;
        }

        // ── Esperamos el documento ──────────────────────────────────────────
        if (string.IsNullOrEmpty(mensaje.MediaId))
        {
            await _whatsApp.SendTextAsync(mensaje.PhoneNumber,
                "📄 Por favor envía tu *Constancia de Situación Fiscal* como archivo PDF o foto del documento.\n\n" +
                "Si prefieres capturar manualmente, escribe *cancelar* y vuelve al menú.");
            return;
        }

        var tipoArchivo = mensaje.MediaMimeType ?? string.Empty;
        if (!tipoArchivo.Contains("pdf") && !tipoArchivo.Contains("image"))
        {
            await _whatsApp.SendTextAsync(mensaje.PhoneNumber,
                "⚠️ El archivo debe ser PDF o imagen (JPG/PNG).\n\n" +
                "Por favor envía tu Constancia de Situación Fiscal en uno de esos formatos.");
            return;
        }

        await _whatsApp.SendTextAsync(mensaje.PhoneNumber,
            "⏳ Analizando tu Constancia de Situación Fiscal...");

        try
        {
            var (stream, _) = await _whatsApp.DownloadMediaAsync(mensaje.MediaId);
            var datos = await _azureService.AnalyzeConstanciaAsync(stream, mensaje.MediaMimeType);

            if (!datos.Exitoso)
            {
                _logger.LogWarning("OCR constancia falló para {Phone}: {Error}", mensaje.PhoneNumber, datos.ErrorMensaje);
                await _conversationRepo.UpdateAsync(
                    mensaje.PhoneNumber,
                    FlowType.SolicitarFactura,
                    StepType.Factura_RazonSocial,
                    JsonSerializer.Serialize(tempData));

                await _whatsApp.SendTextAsync(mensaje.PhoneNumber,
                    "⚠️ No pude leer los datos de tu constancia. Por favor ingresa los datos manualmente.\n\n" +
                    "¿Cuál es tu *razón social o nombre completo* tal como aparece en el SAT?");
                return;
            }

            // Validar que el RFC extraído coincida con el ingresado por el usuario
            if (!string.IsNullOrEmpty(datos.RFC) &&
                !datos.RFC.Equals(tempData.RFC, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("RFC mismatch en constancia para {Phone}: ingresado={UserRFC}, constancia={OcrRFC}",
                    mensaje.PhoneNumber, tempData.RFC, datos.RFC);
                // Continuamos pero usamos el RFC que el usuario escribió (ya fue validado)
            }

            // Llenar tempData con los datos extraídos por OCR
            if (!string.IsNullOrEmpty(datos.RazonSocial))
                tempData.RazonSocial = datos.RazonSocial;
            if (!string.IsNullOrEmpty(datos.RegimenFiscalClave))
            {
                tempData.RegimenFiscalClave = datos.RegimenFiscalClave;
                tempData.RegimenFiscalDescripcion = datos.RegimenFiscalDescripcion;
            }
            if (!string.IsNullOrEmpty(datos.CodigoPostal))
                tempData.CodigoPostal = datos.CodigoPostal;

            tempData.DatosDesdeConstancia = true;
            tempData.ConstanciaConfianza = datos.Confianza;
            tempData.ConstanciaProcesada = true;

            await _conversationRepo.UpdateAsync(
                mensaje.PhoneNumber,
                FlowType.SolicitarFactura,
                StepType.Factura_EsperandoConstancia,
                JsonSerializer.Serialize(tempData));

            await MostrarResumenConstanciaAsync(mensaje.PhoneNumber, tempData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al procesar constancia para {Phone}", mensaje.PhoneNumber);

            await _conversationRepo.UpdateAsync(
                mensaje.PhoneNumber,
                FlowType.SolicitarFactura,
                StepType.Factura_RazonSocial,
                JsonSerializer.Serialize(tempData));

            await _whatsApp.SendTextAsync(mensaje.PhoneNumber,
                "⚠️ Ocurrió un error al procesar el documento. Por favor ingresa los datos manualmente.\n\n" +
                "¿Cuál es tu *razón social o nombre completo*?");
        }
    }

    private async Task MostrarResumenConstanciaAsync(string phoneNumber, FacturaTempData tempData)
    {
        var sb = new StringBuilder();
        sb.AppendLine("📄 *Datos extraídos de tu Constancia de Situación Fiscal:*\n");
        sb.AppendLine($"🔑 RFC: {tempData.RFC ?? "No detectado"}");
        sb.AppendLine($"🏢 Razón social: {tempData.RazonSocial ?? "No detectado"}");

        var regimen = tempData.RegimenFiscalClave != null
            ? $"{tempData.RegimenFiscalClave} - {tempData.RegimenFiscalDescripcion}"
            : "No detectado";
        sb.AppendLine($"📊 Régimen fiscal: {regimen}");
        sb.AppendLine($"📮 Código postal: {tempData.CodigoPostal ?? "No detectado"}");
        sb.AppendLine();
        sb.Append("¿Son correctos estos datos?");

        await _whatsApp.SendButtonsAsync(phoneNumber, sb.ToString(),
        [
            ("CONST_CONFIRMAR", "✅ Sí, correcto"),
            ("CONST_EDITAR", "✏️ Corregir datos")
        ]);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static FacturaTempData DeserializarTempData(string? json)
    {
        if (string.IsNullOrEmpty(json)) return new FacturaTempData();
        return JsonSerializer.Deserialize<FacturaTempData>(json, _jsonOptions) ?? new FacturaTempData();
    }
}
