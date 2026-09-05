namespace botFacturacion.Domain.Enums;

/// <summary>
/// Estados del ciclo de vida de una solicitud de factura.
/// </summary>
public enum EstadoSolicitud
{
    /// <summary>Recién creada, pendiente de revisión por el contador.</summary>
    Pendiente = 0,

    /// <summary>El contador está procesando la factura.</summary>
    EnProceso = 1,

    /// <summary>CFDI generado y enviado al solicitante.</summary>
    Facturado = 2,

    /// <summary>Solicitud cancelada.</summary>
    Cancelado = 3
}

/// <summary>
/// Estados posibles de una cita médica.
/// </summary>
public enum EstadoCita
{
    /// <summary>Cita confirmada y pendiente de realizar.</summary>
    Confirmada = 0,

    /// <summary>Cita cancelada por el paciente o el consultorio.</summary>
    Cancelada = 1,

    /// <summary>El paciente asistió a la cita.</summary>
    Realizada = 2,

    /// <summary>El paciente no se presentó.</summary>
    NoAsistio = 3
}
