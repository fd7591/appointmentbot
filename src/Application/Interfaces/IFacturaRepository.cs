using botFacturacion.Domain.Entities;

namespace botFacturacion.Application.Interfaces;

/// <summary>
/// Repositorio para gestionar solicitudes de factura y receptores fiscales.
/// </summary>
public interface IFacturaRepository
{
    // ─── ReceptorFiscal ───────────────────────────────────────────────────────

    /// <summary>Busca un receptor fiscal por RFC. Retorna null si no existe.</summary>
    Task<ReceptorFiscal?> GetReceptorByRFCAsync(string rfc);

    /// <summary>Persiste un nuevo receptor fiscal.</summary>
    Task<ReceptorFiscal> CreateReceptorAsync(ReceptorFiscal receptor);

    /// <summary>Actualiza los datos de un receptor fiscal existente.</summary>
    Task<ReceptorFiscal> UpdateReceptorAsync(ReceptorFiscal receptor);

    // ─── SolicitudFactura ─────────────────────────────────────────────────────

    /// <summary>Persiste una nueva solicitud de factura.</summary>
    Task<SolicitudFactura> CreateSolicitudAsync(SolicitudFactura solicitud);

    /// <summary>Obtiene una solicitud por su folio interno.</summary>
    Task<SolicitudFactura?> GetByFolioAsync(string folio);

    /// <summary>Obtiene todas las solicitudes de un teléfono, ordenadas por fecha descendente.</summary>
    Task<IEnumerable<SolicitudFactura>> GetByPhoneAsync(string telefono);

    /// <summary>
    /// Genera el siguiente folio disponible para la fecha actual.
    /// Formato: FACT-YYYYMMDD-XXXX
    /// </summary>
    Task<string> GenerateFolioAsync();

    /// <summary>Actualiza el número de fila en Google Sheets de una solicitud.</summary>
    Task UpdateFilaGoogleSheetsAsync(int solicitudId, int fila);
}
