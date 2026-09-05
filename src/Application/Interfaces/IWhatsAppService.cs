namespace botFacturacion.Application.Interfaces;

/// <summary>
/// Servicio para enviar mensajes a través de WhatsApp Cloud API de Meta.
/// </summary>
public interface IWhatsAppService
{
    /// <summary>Envía un mensaje de texto plano a un número de WhatsApp.</summary>
    Task SendTextAsync(string phoneNumber, string message);

    /// <summary>
    /// Envía un mensaje con botones de respuesta rápida (máximo 3 botones).
    /// Cada botón es un par (id, título) donde el título tiene máximo 20 caracteres.
    /// </summary>
    Task SendButtonsAsync(string phoneNumber, string bodyText, IEnumerable<(string Id, string Title)> buttons);

    /// <summary>
    /// Envía un mensaje con lista desplegable (para más de 3 opciones).
    /// Ideal para listas de regímenes fiscales, horarios, etc.
    /// </summary>
    Task SendListAsync(
        string phoneNumber,
        string bodyText,
        string buttonText,
        string sectionTitle,
        IEnumerable<(string Id, string Title, string Description)> items);

    /// <summary>
    /// Marca un mensaje recibido como leído (doble palomita azul).
    /// Mejora la experiencia del usuario.
    /// </summary>
    Task MarkAsReadAsync(string messageId);

    /// <summary>
    /// Descarga un archivo multimedia de WhatsApp a partir de su media ID.
    /// Realiza dos llamadas a Meta Graph API: obtiene la URL y luego descarga el archivo.
    /// </summary>
    /// <returns>Stream con el contenido del archivo y su MIME type.</returns>
    Task<(Stream Content, string MimeType)> DownloadMediaAsync(string mediaId);
}
