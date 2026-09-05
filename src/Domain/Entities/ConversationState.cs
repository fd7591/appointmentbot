using botFacturacion.Domain.Enums;

namespace botFacturacion.Domain.Entities;

/// <summary>
/// Estado de la conversación de un usuario con el bot.
/// Persiste el flujo activo, el paso actual y los datos temporales recolectados.
/// Se limpia al completar o cancelar un flujo, o por timeout de sesión.
/// </summary>
public class ConversationState
{
    public int Id { get; set; }

    /// <summary>
    /// Número de teléfono de WhatsApp del usuario (índice único).
    /// Incluye código de país sin el '+' (ej. "521XXXXXXXXXX").
    /// </summary>
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>Flujo de conversación activo en este momento.</summary>
    public FlowType CurrentFlow { get; set; } = FlowType.None;

    /// <summary>Paso actual dentro del flujo activo.</summary>
    public StepType CurrentStep { get; set; } = StepType.None;

    /// <summary>
    /// Datos temporales del flujo serializado como JSON.
    /// Contiene los campos recolectados paso a paso antes de la confirmación final.
    /// Ejemplos: CitaTempData o FacturaTempData serializados.
    /// </summary>
    public string? TempData { get; set; }

    /// <summary>
    /// Fecha y hora UTC de la última interacción del usuario.
    /// Se usa para calcular el timeout de sesión (configurable, default 30 min).
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Fecha y hora UTC en que se creó el registro.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
