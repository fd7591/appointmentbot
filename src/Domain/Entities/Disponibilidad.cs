namespace botFacturacion.Domain.Entities;

/// <summary>
/// Define la disponibilidad de horarios del consultorio por día de la semana.
/// Cada registro representa un bloque de tiempo disponible con su capacidad máxima de citas.
/// </summary>
public class Disponibilidad
{
    public int Id { get; set; }

    /// <summary>
    /// Día de la semana (0=Domingo, 1=Lunes, ..., 6=Sábado).
    /// Coincide con DayOfWeek de .NET.
    /// </summary>
    public int DiaSemana { get; set; }

    /// <summary>Hora de inicio del bloque (ej. 09:00).</summary>
    public TimeSpan HoraInicio { get; set; }

    /// <summary>Hora de fin del bloque (ej. 09:30).</summary>
    public TimeSpan HoraFin { get; set; }

    /// <summary>Número máximo de citas permitidas en este bloque.</summary>
    public int MaxCitas { get; set; } = 1;

    /// <summary>Indica si este bloque está habilitado.</summary>
    public bool Activo { get; set; } = true;

    // ─── Propiedad calculada ───────────────────────────────────────

    /// <summary>Nombre amigable del día de la semana en español.</summary>
    public string NombreDia => DiaSemana switch
    {
        0 => "Domingo",
        1 => "Lunes",
        2 => "Martes",
        3 => "Miércoles",
        4 => "Jueves",
        5 => "Viernes",
        6 => "Sábado",
        _ => "Desconocido"
    };

    /// <summary>Representación del horario en formato HH:mm.</summary>
    public string HorarioTexto =>
        $"{HoraInicio:hh\\:mm} - {HoraFin:hh\\:mm}";
}
