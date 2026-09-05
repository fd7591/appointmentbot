namespace botFacturacion.Application.DTOs;

/// <summary>
/// Datos temporales del flujo "Agendar Cita".
/// Se serializan como JSON en ConversationState.TempData.
/// </summary>
public class CitaTempData
{
    public string? PacienteNombre { get; set; }

    /// <summary>Teléfono de contacto del paciente (10 dígitos, sin código de país).</summary>
    public string? PacienteTelefono { get; set; }

    /// <summary>Correo electrónico del paciente.</summary>
    public string? PacienteEmail { get; set; }

    /// <summary>Fecha seleccionada en formato ISO (yyyy-MM-dd).</summary>
    public string? FechaCita { get; set; }

    /// <summary>Hora de la cita en formato HH:mm.</summary>
    public string? HoraCita { get; set; }

    public string? Motivo { get; set; }
}

/// <summary>
/// Datos temporales del flujo "Solicitar Factura".
/// Se serializan como JSON en ConversationState.TempData.
/// </summary>
public class FacturaTempData
{
    // ── Datos fiscales del receptor ──────────────────────────────
    public string? RFC { get; set; }
    public string? RazonSocial { get; set; }

    /// <summary>Clave del régimen fiscal SAT (ej. "626").</summary>
    public string? RegimenFiscalClave { get; set; }
    public string? RegimenFiscalDescripcion { get; set; }
    public string? CodigoPostal { get; set; }
    public string? Email { get; set; }

    /// <summary>
    /// true si el usuario eligió usar los datos fiscales ya guardados en BD.
    /// false si ingresó datos nuevos o los actualizó.
    /// </summary>
    public bool UsarDatosGuardados { get; set; } = false;

    /// <summary>Id del ReceptorFiscal en BD (si ya existía).</summary>
    public int? ReceptorFiscalId { get; set; }

    /// <summary>true si los datos fiscales fueron extraídos de la Constancia de Situación Fiscal via OCR.</summary>
    public bool DatosDesdeConstancia { get; set; } = false;

    /// <summary>Nivel de confianza (0-1) del OCR al extraer datos de la constancia. Null si se ingresaron manualmente.</summary>
    public float? ConstanciaConfianza { get; set; }

    /// <summary>true si ya se procesó la constancia y estamos esperando confirmación del usuario.</summary>
    public bool ConstanciaProcesada { get; set; } = false;

    // ── Datos de la consulta a facturar ──────────────────────────
    /// <summary>Fecha de la consulta en formato ISO (yyyy-MM-dd).</summary>
    public string? FechaConsulta { get; set; }

    public decimal? Monto { get; set; }

    /// <summary>Clave SAT del método de pago (ej. "01").</summary>
    public string? MetodoPagoClave { get; set; }
    public string? MetodoPagoDescripcion { get; set; }

    /// <summary>"PUE" o "PPD".</summary>
    public string? FormaPago { get; set; }

    /// <summary>Clave SAT del uso del CFDI (ej. "D01").</summary>
    public string? UsoCFDIClave { get; set; }
    public string? UsoCFDIDescripcion { get; set; }
}
