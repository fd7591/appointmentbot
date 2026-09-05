namespace botFacturacion.Domain.Enums;

/// <summary>
/// Tipos de flujo de conversación disponibles en el bot.
/// </summary>
public enum FlowType
{
    /// <summary>Sin flujo activo — menú principal.</summary>
    None = 0,

    /// <summary>Flujo para agendar cita con el doctor.</summary>
    AgendarCita = 1,

    /// <summary>Flujo para solicitar factura.</summary>
    SolicitarFactura = 2,

    /// <summary>Flujo para consultar citas existentes.</summary>
    ConsultarCitas = 3
}
