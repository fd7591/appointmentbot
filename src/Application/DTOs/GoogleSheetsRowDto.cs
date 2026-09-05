namespace botFacturacion.Application.DTOs;

/// <summary>
/// Representación de una fila a escribir en Google Sheets.
/// El orden de las propiedades corresponde al orden de columnas A–S.
/// </summary>
public class GoogleSheetsRowDto
{
    /// <summary>A: Folio interno (FACT-YYYYMMDD-XXXX).</summary>
    public string Folio { get; set; } = string.Empty;

    /// <summary>B: Fecha y hora de la solicitud (timestamp).</summary>
    public string FechaSolicitud { get; set; } = string.Empty;

    /// <summary>C: Teléfono de WhatsApp del solicitante.</summary>
    public string TelefonoSolicitante { get; set; } = string.Empty;

    /// <summary>D: RFC del receptor.</summary>
    public string RFC { get; set; } = string.Empty;

    /// <summary>E: Razón social o nombre completo.</summary>
    public string RazonSocial { get; set; } = string.Empty;

    /// <summary>F: Clave del régimen fiscal (ej. "626").</summary>
    public string RegimenFiscalClave { get; set; } = string.Empty;

    /// <summary>G: Descripción del régimen fiscal.</summary>
    public string RegimenFiscalDescripcion { get; set; } = string.Empty;

    /// <summary>H: Código postal del domicilio fiscal.</summary>
    public string CodigoPostal { get; set; } = string.Empty;

    /// <summary>I: Correo electrónico del receptor.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>J: Fecha de la consulta (DD/MM/YYYY).</summary>
    public string FechaConsulta { get; set; } = string.Empty;

    /// <summary>K: Monto pagado.</summary>
    public string Monto { get; set; } = string.Empty;

    /// <summary>L: Clave del método de pago (ej. "01").</summary>
    public string MetodoPagoClave { get; set; } = string.Empty;

    /// <summary>M: Descripción del método de pago.</summary>
    public string MetodoPagoDescripcion { get; set; } = string.Empty;

    /// <summary>N: Forma de pago (PUE / PPD).</summary>
    public string FormaPago { get; set; } = string.Empty;

    /// <summary>O: Clave del uso del CFDI (ej. "D01").</summary>
    public string UsoCFDIClave { get; set; } = string.Empty;

    /// <summary>P: Descripción del uso del CFDI.</summary>
    public string UsoCFDIDescripcion { get; set; } = string.Empty;

    /// <summary>Q: Estado de la solicitud (PENDIENTE / EN_PROCESO / FACTURADO / CANCELADO).</summary>
    public string Estado { get; set; } = "PENDIENTE";

    /// <summary>R: Notas del contador (vacío al crear).</summary>
    public string NotasContador { get; set; } = string.Empty;

    /// <summary>S: Folio fiscal UUID del CFDI timbrado (vacío al crear — se llena en Fase 2).</summary>
    public string FolioFiscalUUID { get; set; } = string.Empty;

    /// <summary>Convierte el DTO a una lista de objetos para la API de Google Sheets.</summary>
    public IList<object> ToRowValues() =>
    [
        Folio,
        FechaSolicitud,
        TelefonoSolicitante,
        RFC,
        RazonSocial,
        RegimenFiscalClave,
        RegimenFiscalDescripcion,
        CodigoPostal,
        Email,
        FechaConsulta,
        Monto,
        MetodoPagoClave,
        MetodoPagoDescripcion,
        FormaPago,
        UsoCFDIClave,
        UsoCFDIDescripcion,
        Estado,
        NotasContador,
        FolioFiscalUUID
    ];
}
