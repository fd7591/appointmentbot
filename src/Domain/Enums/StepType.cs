namespace botFacturacion.Domain.Enums;

/// <summary>
/// Pasos individuales dentro de cada flujo de conversación.
/// Los valores están agrupados por rango para facilitar la navegación:
///   0-99   → General
///   100-199 → Flujo AgendarCita
///   200-299 → Flujo SolicitarFactura
///   300-399 → Flujo ConsultarCitas
/// </summary>
public enum StepType
{
    // ─── General ──────────────────────────────────────────────
    None = 0,
    MainMenu = 1,

    // ─── Agendar Cita ─────────────────────────────────────────
    /// <summary>Esperando nombre completo del paciente (mínimo nombre + apellido).</summary>
    Cita_Nombre = 100,

    /// <summary>Esperando selección de fecha (próximos 7 días).</summary>
    Cita_Fecha = 101,

    /// <summary>Esperando selección de horario disponible.</summary>
    Cita_Horario = 102,

    /// <summary>Esperando motivo de consulta (texto libre).</summary>
    Cita_Motivo = 103,

    /// <summary>Esperando confirmación final (Sí / No).</summary>
    Cita_Confirmacion = 104,

    /// <summary>Esperando número de teléfono de contacto del paciente.</summary>
    Cita_Telefono = 105,

    /// <summary>Esperando correo electrónico del paciente.</summary>
    Cita_Email = 106,

    /// <summary>Usuario identificado — esperando confirmación de usar datos registrados.</summary>
    Cita_ConfirmarPerfil = 107,

    /// <summary>Usuario quiere cambiar teléfono — esperando ingreso manual de 10 dígitos.</summary>
    Cita_TelefonoInput = 108,

    // ─── Solicitar Factura ────────────────────────────────────
    /// <summary>Esperando ingreso de RFC del receptor.</summary>
    Factura_RFC = 200,

    /// <summary>RFC encontrado — esperando confirmación de usar datos guardados.</summary>
    Factura_ConfirmacionDatosFiscales = 201,

    /// <summary>RFC nuevo — esperando razón social o nombre completo.</summary>
    Factura_RazonSocial = 202,

    /// <summary>Esperando selección de régimen fiscal.</summary>
    Factura_RegimenFiscal = 203,

    /// <summary>Esperando código postal del domicilio fiscal.</summary>
    Factura_CodigoPostal = 204,

    /// <summary>Esperando correo electrónico para envío del CFDI.</summary>
    Factura_Email = 205,

    /// <summary>Esperando fecha de la consulta a facturar.</summary>
    Factura_FechaConsulta = 206,

    /// <summary>Esperando monto pagado.</summary>
    Factura_Monto = 207,

    /// <summary>Esperando selección de método de pago (clave SAT).</summary>
    Factura_MetodoPago = 208,

    /// <summary>Esperando forma de pago (PUE / PPD).</summary>
    Factura_FormaPago = 209,

    /// <summary>Esperando selección de uso del CFDI.</summary>
    Factura_UsoCFDI = 210,

    /// <summary>Mostrando resumen — esperando confirmación final.</summary>
    Factura_ConfirmacionFinal = 211,

    /// <summary>RFC nuevo — preguntando si ingresa manualmente o sube Constancia de Situación Fiscal.</summary>
    Factura_OpcionIngresoDatos = 212,

    /// <summary>Esperando que el usuario envíe su Constancia de Situación Fiscal (PDF o imagen).</summary>
    Factura_EsperandoConstancia = 213,

    // ─── Consultar Citas ──────────────────────────────────────
    ConsultarCitas_Listado = 300
}
