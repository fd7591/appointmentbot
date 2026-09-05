namespace botFacturacion.Application.Interfaces;

/// <summary>
/// Resultado de analizar una Constancia de Situación Fiscal con Azure Document Intelligence.
/// </summary>
public class ConstanciaFiscalDto
{
    /// <summary>RFC extraído del documento.</summary>
    public string? RFC { get; set; }

    /// <summary>
    /// Razón social o nombre completo extraído.
    /// Para persona física se construye como: PRIMER_APELLIDO SEGUNDO_APELLIDO NOMBRE(S).
    /// </summary>
    public string? RazonSocial { get; set; }

    /// <summary>Clave SAT del régimen fiscal (ej. "626"). Null si no se detectó.</summary>
    public string? RegimenFiscalClave { get; set; }

    /// <summary>Descripción del régimen fiscal según el catálogo SAT.</summary>
    public string? RegimenFiscalDescripcion { get; set; }

    /// <summary>Código postal del domicilio fiscal (5 dígitos).</summary>
    public string? CodigoPostal { get; set; }

    /// <summary>Proporción de campos extraídos con éxito (0.0 – 1.0).</summary>
    public float Confianza { get; set; }

    /// <summary>true si se extrajeron al menos RFC + un campo adicional.</summary>
    public bool Exitoso { get; set; }

    /// <summary>Mensaje de error si <see cref="Exitoso"/> es false.</summary>
    public string? ErrorMensaje { get; set; }
}

/// <summary>
/// Servicio que analiza una Constancia de Situación Fiscal usando Azure Document Intelligence
/// y extrae los datos fiscales necesarios para pre-llenar el flujo de solicitud de factura.
/// </summary>
public interface IAzureDocumentIntelligenceService
{
    /// <summary>
    /// Analiza el stream de un documento (PDF o imagen) de Constancia de Situación Fiscal
    /// y extrae RFC, Razón Social, Régimen Fiscal y Código Postal.
    /// </summary>
    /// <param name="documentStream">Stream del archivo (PDF o imagen). Debe ser seekable.</param>
    /// <param name="mimeType">MIME type del archivo (ej. "application/pdf", "image/jpeg").</param>
    Task<ConstanciaFiscalDto> AnalyzeConstanciaAsync(Stream documentStream, string? mimeType = null);
}
