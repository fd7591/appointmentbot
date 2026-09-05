using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using botFacturacion.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace botFacturacion.Infrastructure.ExternalServices;

/// <summary>
/// Configuración para la integración con WhatsApp Cloud API de Meta.
/// </summary>
public class WhatsAppOptions
{
    public const string SectionName = "WhatsApp";

    /// <summary>ID del número de teléfono del negocio en Meta (Phone Number ID).</summary>
    public string PhoneNumberId { get; set; } = string.Empty;

    /// <summary>Token de acceso permanente o temporal de la app de Meta.</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>Token para verificar el webhook al configurarlo en Meta for Developers.</summary>
    public string VerifyToken { get; set; } = string.Empty;

    /// <summary>Versión de la API de Graph (default: v19.0).</summary>
    public string ApiVersion { get; set; } = "v19.0";
}

/// <summary>
/// Implementación del servicio de WhatsApp usando la Cloud API de Meta.
/// Envía mensajes de texto, botones interactivos y listas desplegables.
/// </summary>
public class WhatsAppService : IWhatsAppService
{
    private readonly HttpClient _httpClient;
    private readonly WhatsAppOptions _options;
    private readonly ILogger<WhatsAppService> _logger;

    // Límite de caracteres en título de botón de WhatsApp
    private const int MaxButtonTitleLength = 20;
    // Máximo de botones por mensaje interactivo de tipo "button"
    private const int MaxButtons = 3;

    public WhatsAppService(
        HttpClient httpClient,
        IOptions<WhatsAppOptions> options,
        ILogger<WhatsAppService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        // Configurar el encabezado de autorización Bearer
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _options.AccessToken);
    }

    // ─── Endpoint base ────────────────────────────────────────────────────────

    private string MessagesUrl =>
        $"https://graph.facebook.com/{_options.ApiVersion}/{_options.PhoneNumberId}/messages";

    // ─── Enviar texto plano ───────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task SendTextAsync(string phoneNumber, string message)
    {
        try
        {
            var payload = new
            {
                messaging_product = "whatsapp",
                recipient_type = "individual",
                to = phoneNumber,
                type = "text",
                text = new { preview_url = false, body = message }
            };

            await PostAsync(payload, phoneNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al enviar mensaje de texto a {Phone}", phoneNumber);
            throw;
        }
    }

    // ─── Enviar botones interactivos ──────────────────────────────────────────

    /// <inheritdoc/>
    public async Task SendButtonsAsync(
        string phoneNumber,
        string bodyText,
        IEnumerable<(string Id, string Title)> buttons)
    {
        try
        {
            var buttonList = buttons.Take(MaxButtons).ToList();

            if (!buttonList.Any())
            {
                // Fallback a mensaje de texto si no hay botones
                await SendTextAsync(phoneNumber, bodyText);
                return;
            }

            var payload = new
            {
                messaging_product = "whatsapp",
                recipient_type = "individual",
                to = phoneNumber,
                type = "interactive",
                interactive = new
                {
                    type = "button",
                    body = new { text = TruncateText(bodyText, 1024) },
                    action = new
                    {
                        buttons = buttonList.Select(b => new
                        {
                            type = "reply",
                            reply = new
                            {
                                id = b.Id,
                                title = TruncateText(b.Title, MaxButtonTitleLength)
                            }
                        }).ToArray()
                    }
                }
            };

            await PostAsync(payload, phoneNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al enviar botones a {Phone}", phoneNumber);
            throw;
        }
    }

    // ─── Enviar lista desplegable ─────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task SendListAsync(
        string phoneNumber,
        string bodyText,
        string buttonText,
        string sectionTitle,
        IEnumerable<(string Id, string Title, string Description)> items)
    {
        try
        {
            var itemList = items.Take(10).ToList(); // WhatsApp permite máximo 10 ítems por sección

            if (!itemList.Any())
            {
                await SendTextAsync(phoneNumber, bodyText);
                return;
            }

            var payload = new
            {
                messaging_product = "whatsapp",
                recipient_type = "individual",
                to = phoneNumber,
                type = "interactive",
                interactive = new
                {
                    type = "list",
                    body = new { text = TruncateText(bodyText, 1024) },
                    action = new
                    {
                        button = TruncateText(buttonText, 20),
                        sections = new[]
                        {
                            new
                            {
                                title = TruncateText(sectionTitle, 24),
                                rows = itemList.Select(item => new
                                {
                                    id = item.Id,
                                    title = TruncateText(item.Title, 24),
                                    description = TruncateText(item.Description, 72)
                                }).ToArray()
                            }
                        }
                    }
                }
            };

            await PostAsync(payload, phoneNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al enviar lista a {Phone}", phoneNumber);
            throw;
        }
    }

    // ─── Marcar como leído ────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task MarkAsReadAsync(string messageId)
    {
        try
        {
            var payload = new
            {
                messaging_product = "whatsapp",
                status = "read",
                message_id = messageId
            };

            await PostAsync(payload, "N/A");
        }
        catch (Exception ex)
        {
            // Marcar como leído es opcional; no interrumpimos el flujo si falla
            _logger.LogWarning(ex, "No se pudo marcar mensaje {MessageId} como leído", messageId);
        }
    }

    // ─── Descargar media ──────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<(Stream Content, string MimeType)> DownloadMediaAsync(string mediaId)
    {
        try
        {
            // Paso 1: obtener URL de descarga y MIME type desde Graph API
            var metaUrl = $"https://graph.facebook.com/{_options.ApiVersion}/{mediaId}";
            var metaResponse = await _httpClient.GetAsync(metaUrl);
            metaResponse.EnsureSuccessStatusCode();

            var metaJson = await metaResponse.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(metaJson);
            var downloadUrl = doc.RootElement.GetProperty("url").GetString()
                ?? throw new InvalidOperationException("La respuesta de Meta no contiene la URL del archivo.");
            var mimeType = doc.RootElement.TryGetProperty("mime_type", out var mt)
                ? mt.GetString() ?? "application/octet-stream"
                : "application/octet-stream";

            // Paso 2: descargar el archivo (el Bearer token es necesario también aquí)
            var fileResponse = await _httpClient.GetAsync(downloadUrl);
            fileResponse.EnsureSuccessStatusCode();

            // Copiar a MemoryStream para que sea seekable
            var memStream = new MemoryStream();
            await fileResponse.Content.CopyToAsync(memStream);
            memStream.Seek(0, SeekOrigin.Begin);

            _logger.LogDebug("Media {MediaId} descargado exitosamente ({Bytes} bytes, {Mime})",
                mediaId, memStream.Length, mimeType);

            return (memStream, mimeType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al descargar media {MediaId}", mediaId);
            throw;
        }
    }

    // ─── Helper interno para POST ─────────────────────────────────────────────

    private async Task PostAsync(object payload, string phoneNumber)
    {
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        _logger.LogDebug("Enviando a WhatsApp [{Phone}]: {Json}", phoneNumber, json);

        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(MessagesUrl, content);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            _logger.LogError("WhatsApp API retornó {Status} para {Phone}: {Error}",
                (int)response.StatusCode, phoneNumber, errorBody);
            response.EnsureSuccessStatusCode(); // Lanza HttpRequestException con el código de estado
        }

        _logger.LogDebug("Mensaje enviado exitosamente a {Phone}", phoneNumber);
    }

    // ─── Helper para truncar texto ────────────────────────────────────────────

    private static string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength) return text;
        return text[..(maxLength - 1)] + "…";
    }
}
