using botFacturacion.Domain.Enums;

namespace botFacturacion.Domain.Entities;

/// <summary>
/// Datos fiscales de un receptor de CFDI.
/// Se guardan para no pedirlos en solicitudes futuras del mismo RFC.
/// </summary>
public class ReceptorFiscal
{
    public int Id { get; set; }

    /// <summary>
    /// RFC del receptor validado con el patrón del SAT.
    /// Persona física: 13 caracteres. Persona moral: 12 caracteres.
    /// </summary>
    public string RFC { get; set; } = string.Empty;

    /// <summary>Razón social (personas morales) o nombre completo (personas físicas), como aparece en el SAT.</summary>
    public string RazonSocial { get; set; } = string.Empty;

    /// <summary>Clave del régimen fiscal según el catálogo SAT (ej. "626").</summary>
    public string RegimenFiscalClave { get; set; } = string.Empty;

    /// <summary>Descripción del régimen fiscal para referencia rápida.</summary>
    public string RegimenFiscalDescripcion { get; set; } = string.Empty;

    /// <summary>Código postal del domicilio fiscal registrado ante el SAT (5 dígitos).</summary>
    public string CodigoPostal { get; set; } = string.Empty;

    /// <summary>Correo electrónico al que se enviará el CFDI generado.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Teléfono de WhatsApp que registró este RFC por primera vez.</summary>
    public string TelefonoRegistro { get; set; } = string.Empty;

    /// <summary>Fecha y hora UTC del primer registro.</summary>
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;

    /// <summary>Fecha y hora UTC de la última actualización de datos.</summary>
    public DateTime ActualizadoEn { get; set; } = DateTime.UtcNow;

    /// <summary>Solicitudes de factura asociadas a este receptor.</summary>
    public ICollection<SolicitudFactura> Solicitudes { get; set; } = new List<SolicitudFactura>();
}
