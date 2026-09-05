using botFacturacion.Domain.Entities;

namespace botFacturacion.Application.Interfaces;

// ─────────────────────────────────────────────────────────────────────────────
// TODO Fase 2: Integración con PAC (Proveedor Autorizado de Certificación del SAT)
//
// Flujo previsto:
//   1. Contador abre Google Sheets y cambia el Estado de la solicitud a "EN_PROCESO"
//   2. Un job de polling detecta el cambio (o un webhook de Google Sheets lo notifica)
//   3. El sistema obtiene los datos de la solicitud desde BD
//   4. ICFDIBuilder genera el XML del CFDI 4.0
//   5. IPACService timbra el XML contra el PAC configurado
//   6. Se almacena el UUID del CFDI en BD y en Google Sheets (columna S)
//   7. Se envía el PDF + XML al solicitante por WhatsApp
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Contrato para la integración con un PAC (Proveedor Autorizado de Certificación).
/// Implementación pendiente para Fase 2.
/// </summary>
public interface IPACService
{
    /// <summary>
    /// Timbra el CFDI generado contra el PAC y retorna el UUID asignado por el SAT.
    /// </summary>
    /// <param name="solicitud">Solicitud de factura con todos los datos para el CFDI.</param>
    /// <returns>UUID del CFDI timbrado (Folio Fiscal).</returns>
    // TODO Fase 2: Implementar con el PAC elegido (Edicom, SW SaaS, Finkok, etc.)
    Task<string> TimbrarCFDIAsync(SolicitudFactura solicitud);

    /// <summary>
    /// Cancela un CFDI previamente timbrado ante el SAT.
    /// </summary>
    /// <param name="uuid">UUID del CFDI a cancelar.</param>
    /// <param name="motivo">Clave del motivo de cancelación según el SAT (01-04).</param>
    // TODO Fase 2: Implementar cancelación con RFC del sustituto si aplica
    Task CancelarCFDIAsync(string uuid, string motivo);

    /// <summary>
    /// Consulta el estado de un CFDI ante el SAT.
    /// </summary>
    /// <param name="uuid">UUID del CFDI a consultar.</param>
    // TODO Fase 2: Usar para verificar si el CFDI está vigente o cancelado
    Task<string> ConsultarEstadoAsync(string uuid);
}

/// <summary>
/// Contrato para la construcción del XML del CFDI 4.0.
/// Implementación pendiente para Fase 2.
/// </summary>
public interface ICFDIBuilder
{
    /// <summary>
    /// Genera el XML del CFDI 4.0 a partir de los datos de la solicitud y el receptor.
    /// </summary>
    /// <returns>Cadena XML del CFDI sin timbrar (sin complemento TimbreFiscalDigital).</returns>
    // TODO Fase 2: Validar con el esquema XSD del SAT para CFDI 4.0
    // TODO Fase 2: Incluir cálculo de IEPS si aplica para el tipo de servicio
    Task<string> BuildXmlAsync(SolicitudFactura solicitud, ReceptorFiscal receptor);

    /// <summary>
    /// Genera el PDF del CFDI a partir del XML timbrado.
    /// </summary>
    /// <param name="xmlTimbrado">XML con el complemento TimbreFiscalDigital del PAC.</param>
    /// <returns>Bytes del PDF generado.</returns>
    // TODO Fase 2: Usar una plantilla visual del consultorio (logo, colores)
    Task<byte[]> GeneratePdfAsync(string xmlTimbrado);
}
