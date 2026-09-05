using botFacturacion.Domain.Enums;

namespace botFacturacion.Domain.Entities;

/// <summary>
/// Representa una cita médica agendada por un paciente a través del bot.
/// </summary>
public class Cita
{
    public int Id { get; set; }

    /// <summary>Nombre completo del paciente como lo ingresó en el bot.</summary>
    public string PacienteNombre { get; set; } = string.Empty;

    /// <summary>Número de teléfono de WhatsApp del paciente (incluye código de país, ej. 521XXXXXXXXXX).</summary>
    public string PacienteTelefono { get; set; } = string.Empty;

    /// <summary>Fecha en que está programada la cita.</summary>
    public DateOnly FechaCita { get; set; }

    /// <summary>Hora de inicio de la cita.</summary>
    public TimeSpan HoraCita { get; set; }

    /// <summary>Teléfono de contacto del paciente (10 dígitos).</summary>
    public string? PacienteTelefonoContacto { get; set; }

    /// <summary>Correo electrónico del paciente para confirmaciones.</summary>
    public string? PacienteEmail { get; set; }

    /// <summary>Motivo de la consulta, expresado libremente por el paciente.</summary>
    public string Motivo { get; set; } = string.Empty;

    /// <summary>Estado actual de la cita.</summary>
    public EstadoCita Estado { get; set; } = EstadoCita.Confirmada;

    /// <summary>
    /// Folio de confirmación generado automáticamente.
    /// Formato: CITA-YYYYMMDD-XXXX (ej. CITA-20250519-0001)
    /// </summary>
    public string FolioConfirmacion { get; set; } = string.Empty;

    /// <summary>Fecha y hora UTC en que se creó el registro.</summary>
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;

    /// <summary>Indica si ya se envió el recordatorio automático de 24 horas antes.</summary>
    public bool RecordatorioEnviado { get; set; } = false;

    /// <summary>Relación al doctor (opcional, se puede asignar si hay varios doctores).</summary>
    public int? DoctorId { get; set; }
    public Doctor? Doctor { get; set; }
}
