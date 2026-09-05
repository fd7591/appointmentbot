using botFacturacion.Domain.Entities;

namespace botFacturacion.Application.Interfaces;

public interface IGoogleCalendarService
{
    /// <summary>
    /// Retorna los horarios disponibles (HH:mm) para una fecha dada,
    /// cruzando los horarios de atención con los eventos ya agendados en el calendario.
    /// </summary>
    Task<IReadOnlyList<string>> GetAvailableSlotsAsync(DateOnly fecha);

    /// <summary>
    /// Crea un evento en Google Calendar para la cita confirmada.
    /// </summary>
    Task CreateEventAsync(Cita cita);
}
