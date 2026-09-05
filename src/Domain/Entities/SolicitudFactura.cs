using botFacturacion.Domain.Enums;

namespace botFacturacion.Domain.Entities;

/// <summary>
/// Registro de una solicitud de factura realizada a través del bot.
/// Los datos se sincronizan con Google Sheets para que el contador pueda procesarlos.
/// </summary>
public class SolicitudFactura
{
    public int Id { get; set; }

    /// <summary>
    /// Folio interno autogenerado.
    /// Formato: FACT-YYYYMMDD-XXXX (ej. FACT-20250519-0001)
    /// </summary>
    public string Folio { get; set; } = string.Empty;

    /// <summary>Teléfono de WhatsApp del solicitante.</summary>
    public string TelefonoSolicitante { get; set; } = string.Empty;

    // ─── Datos del receptor fiscal ─────────────────────────────────
    public int ReceptorFiscalId { get; set; }
    public ReceptorFiscal ReceptorFiscal { get; set; } = null!;

    // ─── Datos de la consulta a facturar ──────────────────────────
    /// <summary>Fecha de la consulta médica que se va a facturar.</summary>
    public DateOnly FechaConsulta { get; set; }

    /// <summary>Monto total pagado por la consulta.</summary>
    public decimal Monto { get; set; }

    /// <summary>Clave SAT del método de pago (ej. "01" = Efectivo).</summary>
    public string MetodoPagoClave { get; set; } = string.Empty;

    /// <summary>Descripción del método de pago para referencia rápida.</summary>
    public string MetodoPagoDescripcion { get; set; } = string.Empty;

    /// <summary>Forma de pago: PUE (Pago en Una Exhibición) o PPD (Pago en Parcialidades).</summary>
    public string FormaPago { get; set; } = "PUE";

    /// <summary>Clave SAT del uso del CFDI (ej. "D01" = Honorarios médicos).</summary>
    public string UsoCFDIClave { get; set; } = string.Empty;

    /// <summary>Descripción del uso del CFDI para referencia rápida.</summary>
    public string UsoCFDIDescripcion { get; set; } = string.Empty;

    // ─── Estado y seguimiento ─────────────────────────────────────
    /// <summary>Estado actual de la solicitud.</summary>
    public EstadoSolicitud Estado { get; set; } = EstadoSolicitud.Pendiente;

    /// <summary>Notas del contador (se actualiza en Google Sheets y se sincroniza).</summary>
    public string? NotasContador { get; set; }

    /// <summary>
    /// UUID del CFDI timbrado.
    /// Se llena en Fase 2 cuando el PAC timbra el CFDI.
    /// </summary>
    public string? FolioFiscalUUID { get; set; }

    /// <summary>Número de fila en Google Sheets para facilitar la actualización.</summary>
    public int? FilaGoogleSheets { get; set; }

    /// <summary>Fecha y hora UTC en que se creó la solicitud.</summary>
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;

    /// <summary>Fecha y hora UTC de la última actualización del estado.</summary>
    public DateTime ActualizadoEn { get; set; } = DateTime.UtcNow;
}
