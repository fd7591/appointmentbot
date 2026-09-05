using System.Text.Json.Serialization;

namespace botFacturacion.Application.DTOs;

// ─── DTOs de entrada (Webhook de Meta) ────────────────────────────────────────

/// <summary>Payload raíz del webhook de WhatsApp Cloud API.</summary>
public class WhatsAppWebhookPayload
{
    [JsonPropertyName("object")]
    public string Object { get; set; } = string.Empty;

    [JsonPropertyName("entry")]
    public List<WhatsAppEntry> Entry { get; set; } = new();
}

public class WhatsAppEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("changes")]
    public List<WhatsAppChange> Changes { get; set; } = new();
}

public class WhatsAppChange
{
    [JsonPropertyName("value")]
    public WhatsAppValue Value { get; set; } = new();

    [JsonPropertyName("field")]
    public string Field { get; set; } = string.Empty;
}

public class WhatsAppValue
{
    [JsonPropertyName("messaging_product")]
    public string MessagingProduct { get; set; } = string.Empty;

    [JsonPropertyName("metadata")]
    public WhatsAppMetadata Metadata { get; set; } = new();

    [JsonPropertyName("contacts")]
    public List<WhatsAppContact>? Contacts { get; set; }

    [JsonPropertyName("messages")]
    public List<WhatsAppMessage>? Messages { get; set; }

    [JsonPropertyName("statuses")]
    public List<WhatsAppStatus>? Statuses { get; set; }
}

public class WhatsAppMetadata
{
    [JsonPropertyName("display_phone_number")]
    public string DisplayPhoneNumber { get; set; } = string.Empty;

    [JsonPropertyName("phone_number_id")]
    public string PhoneNumberId { get; set; } = string.Empty;
}

public class WhatsAppContact
{
    [JsonPropertyName("profile")]
    public WhatsAppProfile Profile { get; set; } = new();

    [JsonPropertyName("wa_id")]
    public string WaId { get; set; } = string.Empty;
}

public class WhatsAppProfile
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public class WhatsAppMessage
{
    [JsonPropertyName("from")]
    public string From { get; set; } = string.Empty;

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>Presente cuando type = "text".</summary>
    [JsonPropertyName("text")]
    public WhatsAppText? Text { get; set; }

    /// <summary>Presente cuando type = "interactive".</summary>
    [JsonPropertyName("interactive")]
    public WhatsAppInteractive? Interactive { get; set; }

    /// <summary>Presente cuando type = "document".</summary>
    [JsonPropertyName("document")]
    public WhatsAppMediaInfo? Document { get; set; }

    /// <summary>Presente cuando type = "image".</summary>
    [JsonPropertyName("image")]
    public WhatsAppMediaInfo? Image { get; set; }
}

public class WhatsAppText
{
    [JsonPropertyName("body")]
    public string Body { get; set; } = string.Empty;
}

public class WhatsAppInteractive
{
    /// <summary>Tipo de respuesta interactiva: "button_reply" o "list_reply".</summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("button_reply")]
    public WhatsAppButtonReply? ButtonReply { get; set; }

    [JsonPropertyName("list_reply")]
    public WhatsAppListReply? ListReply { get; set; }
}

public class WhatsAppButtonReply
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;
}

public class WhatsAppListReply
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

/// <summary>Información de un archivo multimedia adjunto (documento o imagen).</summary>
public class WhatsAppMediaInfo
{
    /// <summary>ID del media en Meta — se usa para descargar el archivo vía Graph API.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("mime_type")]
    public string MimeType { get; set; } = string.Empty;

    /// <summary>Nombre original del archivo (solo presente en documentos).</summary>
    [JsonPropertyName("filename")]
    public string? Filename { get; set; }

    [JsonPropertyName("sha256")]
    public string? Sha256 { get; set; }
}

public class WhatsAppStatus
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = string.Empty;

    [JsonPropertyName("recipient_id")]
    public string RecipientId { get; set; } = string.Empty;
}

// ─── DTO de entrada normalizado para el servicio ──────────────────────────────

/// <summary>
/// Mensaje de WhatsApp ya normalizado, extraído del payload del webhook.
/// Es el objeto que circula dentro de la aplicación.
/// </summary>
public class IncomingMessageDto
{
    /// <summary>Número de teléfono del usuario (sin '+').</summary>
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>Nombre del perfil de WhatsApp del usuario.</summary>
    public string ProfileName { get; set; } = string.Empty;

    /// <summary>Tipo de mensaje: "text" o "interactive".</summary>
    public string MessageType { get; set; } = string.Empty;

    /// <summary>Texto del mensaje si type = "text". Cadena vacía si no aplica.</summary>
    public string TextBody { get; set; } = string.Empty;

    /// <summary>
    /// ID del botón o ítem de lista seleccionado si type = "interactive".
    /// Cadena vacía si no aplica.
    /// </summary>
    public string InteractiveId { get; set; } = string.Empty;

    /// <summary>
    /// Título del botón o ítem de lista seleccionado.
    /// Se usa como texto alternativo si el ID no basta.
    /// </summary>
    public string InteractiveTitle { get; set; } = string.Empty;

    /// <summary>Timestamp del mensaje (UTC).</summary>
    public DateTime Timestamp { get; set; }

    // ── Campos de media (document / image) ──────────────────────────────────

    /// <summary>ID del media en Meta Graph API para descargarlo. Presente si type = "document" o "image".</summary>
    public string? MediaId { get; set; }

    /// <summary>MIME type del archivo adjunto (ej. "application/pdf", "image/jpeg").</summary>
    public string? MediaMimeType { get; set; }

    /// <summary>Nombre original del archivo. Presente solo para type = "document".</summary>
    public string? MediaFilename { get; set; }
}
