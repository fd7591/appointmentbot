using botFacturacion.Domain.Entities;
using botFacturacion.Domain.Enums;

namespace botFacturacion.Application.Interfaces;

/// <summary>
/// Repositorio para gestionar el estado de conversación de cada usuario.
/// </summary>
public interface IConversationRepository
{
    /// <summary>
    /// Obtiene el estado de conversación de un usuario.
    /// Si no existe, crea uno nuevo con FlowType.None.
    /// </summary>
    Task<ConversationState> GetOrCreateAsync(string phoneNumber);

    /// <summary>Actualiza el flujo, paso y datos temporales de la conversación.</summary>
    Task UpdateAsync(string phoneNumber, FlowType flow, StepType step, string? tempData = null);

    /// <summary>
    /// Reinicia el estado de la conversación (flujo=None, paso=None, tempData=null).
    /// Se llama al completar o cancelar un flujo, o por timeout.
    /// </summary>
    Task ResetAsync(string phoneNumber);

    /// <summary>
    /// Retorna todas las conversaciones con última actividad anterior al umbral de timeout.
    /// Usado por el job de limpieza de sesiones inactivas.
    /// </summary>
    Task<IEnumerable<ConversationState>> GetExpiredSessionsAsync(DateTime threshold);
}
