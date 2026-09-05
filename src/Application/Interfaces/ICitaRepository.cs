using botFacturacion.Domain.Entities;

namespace botFacturacion.Application.Interfaces;

/// <summary>
/// Repositorio para gestionar citas médicas y disponibilidad de horarios.
/// </summary>
public interface ICitaRepository
{
    // ─── Citas ────────────────────────────────────────────────────────────────

    /// <summary>Persiste una nueva cita en la base de datos.</summary>
    Task<Cita> CreateAsync(Cita cita);

    /// <summary>Obtiene una cita por su folio de confirmación.</summary>
    Task<Cita?> GetByFolioAsync(string folio);

    /// <summary>Obtiene todas las citas de un paciente por su número de teléfono.</summary>
    Task<IEnumerable<Cita>> GetByPhoneAsync(string phoneNumber);

    /// <summary>Obtiene citas confirmadas para un día específico.</summary>
    Task<IEnumerable<Cita>> GetByFechaAsync(DateOnly fecha);

    /// <summary>
    /// Obtiene las citas confirmadas que deben recibir recordatorio.
    /// Condición: mañana es la fecha de la cita y no se ha enviado el recordatorio.
    /// </summary>
    Task<IEnumerable<Cita>> GetPendingRemindersAsync(DateOnly fechaCita);

    /// <summary>Marca una cita como recordatorio enviado.</summary>
    Task MarkReminderSentAsync(int citaId);

    /// <summary>Cancela una cita por su folio.</summary>
    Task CancelAsync(string folio);

    // ─── Disponibilidad ───────────────────────────────────────────────────────

    /// <summary>
    /// Retorna los horarios disponibles para una fecha específica,
    /// descontando los ya ocupados por citas confirmadas.
    /// </summary>
    Task<IEnumerable<string>> GetHorariosDisponiblesAsync(DateOnly fecha);

    /// <summary>Verifica si un horario específico está disponible para una fecha.</summary>
    Task<bool> IsHorarioDisponibleAsync(DateOnly fecha, TimeSpan hora);

    /// <summary>
    /// Genera el siguiente folio disponible para una fecha.
    /// Formato: CITA-YYYYMMDD-XXXX
    /// </summary>
    Task<string> GenerateFolioAsync(DateOnly fecha);
}
