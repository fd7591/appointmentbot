using System.Text.Json;
using botFacturacion.Application.DTOs;
using botFacturacion.Application.Services;
using botFacturacion.Infrastructure.ExternalServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace botFacturacion.API.Controllers;

/// <summary>
/// Controlador principal del webhook de WhatsApp Cloud API.
///
/// Expone dos endpoints:
///   GET  /webhook — Verificación del webhook al configurarlo en Meta for Developers.
///   POST /webhook — Recepción de mensajes y eventos de WhatsApp.
/// </summary>
[ApiController]
[Route("webhook")]
public class WebhookController : ControllerBase
{
    // Se inyecta IServiceScopeFactory en lugar de ConversationService directamente.
    // Esto es necesario porque el procesamiento ocurre en un Task.Run (fire-and-forget)
    // que sobrevive al request HTTP. Si usáramos el servicio Scoped del request,
    // el AppDbContext ya estaría dispuesto cuando el Task intente usarlo.
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly WhatsAppOptions _whatsAppOptions;
    private readonly ILogger<WebhookController> _logger;

    public WebhookController(
        IServiceScopeFactory scopeFactory,
        IOptions<WhatsAppOptions> whatsAppOptions,
        ILogger<WebhookController> logger)
    {
        _scopeFactory = scopeFactory;
        _whatsAppOptions = whatsAppOptions.Value;
        _logger = logger;
    }

    // ─── GET /webhook — Verificación del webhook ──────────────────────────────

    /// <summary>
    /// Meta envía una petición GET con tres parámetros para verificar el webhook.
    /// Si el hub.verify_token coincide con el configurado en appsettings,
    /// se responde con el hub.challenge para completar la verificación.
    /// </summary>
    [HttpGet]
    public IActionResult VerifyWebhook(
        [FromQuery(Name = "hub.mode")] string? hubMode,
        [FromQuery(Name = "hub.verify_token")] string? hubVerifyToken,
        [FromQuery(Name = "hub.challenge")] string? hubChallenge)
    {
        _logger.LogInformation("Solicitud de verificación del webhook recibida. mode={Mode}", hubMode);

        if (hubMode == "subscribe" && hubVerifyToken == _whatsAppOptions.VerifyToken && hubChallenge != null)
        {
            _logger.LogInformation("Webhook verificado exitosamente.");
            return Ok(hubChallenge); // Responder con el challenge en texto plano
        }

        _logger.LogWarning("Verificación del webhook fallida. Token no coincide.");
        return StatusCode(403);
    }

    // ─── POST /webhook — Recepción de mensajes ────────────────────────────────

    /// <summary>
    /// Recibe el payload JSON del webhook de WhatsApp, lo parsea y
    /// delega el procesamiento al ConversationService.
    ///
    /// Meta espera un HTTP 200 en menos de 15 segundos; si no lo recibe,
    /// reintenta el envío. El procesamiento pesado debe ser asíncrono.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> ReceiveMessage()
    {
        string rawBody;
        try
        {
            using var reader = new StreamReader(Request.Body);
            rawBody = await reader.ReadToEndAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al leer el cuerpo del webhook");
            return Ok(); // Siempre retornar 200 para que Meta no reintente
        }

        _logger.LogDebug("Webhook payload recibido: {Body}", rawBody);

        try
        {
            var payload = JsonSerializer.Deserialize<WhatsAppWebhookPayload>(rawBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (payload?.Object != "whatsapp_business_account")
            {
                _logger.LogDebug("Payload ignorado: object={Object}", payload?.Object);
                return Ok();
            }

            // Iterar sobre todas las entradas y cambios del payload
            foreach (var entry in payload.Entry ?? Enumerable.Empty<WhatsAppEntry>())
            {
                foreach (var change in entry.Changes ?? Enumerable.Empty<WhatsAppChange>())
                {
                    if (change.Field != "messages") continue;

                    var messages = change.Value?.Messages;
                    if (messages == null || !messages.Any()) continue;

                    var contacts = change.Value?.Contacts ?? new List<WhatsAppContact>();

                    foreach (var message in messages)
                    {
                        // Normalizar el mensaje a un DTO interno
                        var msgDto = NormalizarMensaje(message, contacts);
                        if (msgDto == null)
                        {
                            _logger.LogDebug("Mensaje ignorado (tipo no soportado): {Type}", message.Type);
                            continue;
                        }

                        // Procesar en background con su propio scope de DI.
                        // Cada mensaje crea un scope nuevo para que AppDbContext
                        // y los servicios Scoped no sean dispuestos cuando el
                        // request HTTP termina antes de que el Task finalice.
                        // Nota: en producción considera una cola (Azure Service Bus, etc.)
                        var msgDtoCopy = msgDto; // captura local segura para el closure
                        _ = Task.Run(async () =>
                        {
                            using var scope = _scopeFactory.CreateScope();
                            var conversationService =
                                scope.ServiceProvider.GetRequiredService<ConversationService>();
                            try
                            {
                                await conversationService.HandleMessageAsync(msgDtoCopy);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex,
                                    "Error en procesamiento asíncrono para {Phone}",
                                    msgDtoCopy.PhoneNumber);
                            }
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al procesar el payload del webhook");
            // No retornar error al cliente; Meta reintentaría innecesariamente
        }

        // Siempre retornar 200 OK a Meta para confirmar recepción
        return Ok();
    }

    // ─── Normalizador de mensaje ───────────────────────────────────────────────

    /// <summary>
    /// Convierte un WhatsAppMessage (crudo del webhook) en un IncomingMessageDto
    /// normalizado para uso interno. Retorna null si el tipo de mensaje no está soportado.
    /// </summary>
    private IncomingMessageDto? NormalizarMensaje(
        WhatsAppMessage message,
        List<WhatsAppContact> contacts)
    {
        // Solo soportamos mensajes de texto, interactivos, documentos e imágenes
        if (message.Type is not ("text" or "interactive" or "document" or "image"))
            return null;

        var nombrePerfil = contacts
            .FirstOrDefault(c => c.WaId == message.From)
            ?.Profile?.Name ?? string.Empty;

        var dto = new IncomingMessageDto
        {
            PhoneNumber = message.From,
            ProfileName = nombrePerfil,
            MessageType = message.Type,
            Timestamp = DateTimeOffset.FromUnixTimeSeconds(
                long.TryParse(message.Timestamp, out var ts) ? ts : 0).UtcDateTime
        };

        switch (message.Type)
        {
            case "text":
                dto.TextBody = message.Text?.Body ?? string.Empty;
                break;

            case "interactive" when message.Interactive?.Type == "button_reply":
                dto.InteractiveId = message.Interactive.ButtonReply?.Id ?? string.Empty;
                dto.InteractiveTitle = message.Interactive.ButtonReply?.Title ?? string.Empty;
                dto.TextBody = dto.InteractiveTitle; // Para que los comandos globales funcionen
                break;

            case "interactive" when message.Interactive?.Type == "list_reply":
                dto.InteractiveId = message.Interactive.ListReply?.Id ?? string.Empty;
                dto.InteractiveTitle = message.Interactive.ListReply?.Title ?? string.Empty;
                dto.TextBody = dto.InteractiveTitle;
                break;

            case "document":
                dto.MediaId = message.Document?.Id ?? string.Empty;
                dto.MediaMimeType = message.Document?.MimeType;
                dto.MediaFilename = message.Document?.Filename;
                dto.TextBody = string.Empty;
                break;

            case "image":
                dto.MediaId = message.Image?.Id ?? string.Empty;
                dto.MediaMimeType = message.Image?.MimeType;
                dto.TextBody = string.Empty;
                break;

            default:
                return null;
        }

        return dto;
    }
}
