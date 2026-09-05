using botFacturacion.Application.DTOs;

namespace botFacturacion.Application.Interfaces;

/// <summary>
/// Servicio para interactuar con Google Sheets como repositorio de solicitudes de factura.
/// Utiliza una Service Account de Google Cloud.
/// </summary>
public interface IGoogleSheetsService
{
    /// <summary>
    /// Agrega una nueva fila al final de la hoja con los datos de la solicitud.
    /// Retorna el número de fila insertada (base 1, incluyendo encabezado).
    /// </summary>
    Task<int> AppendRowAsync(GoogleSheetsRowDto row);

    /// <summary>
    /// Actualiza la columna Q (Estado) de una fila específica.
    /// Útil para sincronizar cambios del contador.
    /// </summary>
    Task UpdateEstadoAsync(int rowNumber, string estado);

    /// <summary>
    /// Actualiza las columnas R (Notas) y S (FolioFiscalUUID) de una fila.
    /// Se usa en Fase 2 después del timbrado del CFDI.
    /// </summary>
    Task UpdateFolioFiscalAsync(int rowNumber, string notas, string folioFiscalUuid);

    /// <summary>
    /// Verifica la conexión con Google Sheets.
    /// Retorna true si la hoja es accesible con las credenciales configuradas.
    /// </summary>
    Task<bool> TestConnectionAsync();
}
