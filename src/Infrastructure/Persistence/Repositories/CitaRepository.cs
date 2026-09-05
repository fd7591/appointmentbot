using botFacturacion.Application.Interfaces;
using botFacturacion.Domain.Entities;
using botFacturacion.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace botFacturacion.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementación del repositorio de citas usando Entity Framework Core.
/// </summary>
public class CitaRepository : ICitaRepository
{
    private readonly AppDbContext _context;
    private readonly ILogger<CitaRepository> _logger;

    public CitaRepository(AppDbContext context, ILogger<CitaRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    // ─── Citas ────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<Cita> CreateAsync(Cita cita)
    {
        try
        {
            _context.Citas.Add(cita);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Cita creada: {Folio}", cita.FolioConfirmacion);
            return cita;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear cita {Folio}", cita.FolioConfirmacion);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<Cita?> GetByFolioAsync(string folio)
    {
        return await _context.Citas
            .Include(c => c.Doctor)
            .FirstOrDefaultAsync(c => c.FolioConfirmacion == folio);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<Cita>> GetByPhoneAsync(string phoneNumber)
    {
        return await _context.Citas
            .Where(c => c.PacienteTelefono == phoneNumber)
            .OrderByDescending(c => c.FechaCita)
            .ThenByDescending(c => c.HoraCita)
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<Cita>> GetByFechaAsync(DateOnly fecha)
    {
        return await _context.Citas
            .Where(c => c.FechaCita == fecha && c.Estado == EstadoCita.Confirmada)
            .OrderBy(c => c.HoraCita)
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<Cita>> GetPendingRemindersAsync(DateOnly fechaCita)
    {
        return await _context.Citas
            .Where(c =>
                c.FechaCita == fechaCita &&
                c.Estado == EstadoCita.Confirmada &&
                !c.RecordatorioEnviado)
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task MarkReminderSentAsync(int citaId)
    {
        var cita = await _context.Citas.FindAsync(citaId);
        if (cita != null)
        {
            cita.RecordatorioEnviado = true;
            await _context.SaveChangesAsync();
        }
    }

    /// <inheritdoc/>
    public async Task CancelAsync(string folio)
    {
        var cita = await _context.Citas.FirstOrDefaultAsync(c => c.FolioConfirmacion == folio);
        if (cita != null)
        {
            cita.Estado = EstadoCita.Cancelada;
            await _context.SaveChangesAsync();
        }
    }

    // ─── Disponibilidad ───────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<IEnumerable<string>> GetHorariosDisponiblesAsync(DateOnly fecha)
    {
        try
        {
            var diaSemana = (int)fecha.DayOfWeek;

            // Obtener todos los bloques activos para ese día
            var bloques = await _context.Disponibilidades
                .Where(d => d.DiaSemana == diaSemana && d.Activo)
                .OrderBy(d => d.HoraInicio)
                .ToListAsync();

            if (!bloques.Any()) return Enumerable.Empty<string>();

            // Obtener citas ya agendadas para esa fecha
            var citasDelDia = await _context.Citas
                .Where(c => c.FechaCita == fecha && c.Estado == EstadoCita.Confirmada)
                .Select(c => c.HoraCita)
                .ToListAsync();

            // Retornar solo los horarios con cupo disponible
            var disponibles = new List<string>();
            foreach (var bloque in bloques)
            {
                var ocupadas = citasDelDia.Count(h => h == bloque.HoraInicio);
                if (ocupadas < bloque.MaxCitas)
                    disponibles.Add($"{bloque.HoraInicio:hh\\:mm}");
            }

            return disponibles;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener horarios disponibles para {Fecha}", fecha);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> IsHorarioDisponibleAsync(DateOnly fecha, TimeSpan hora)
    {
        var diaSemana = (int)fecha.DayOfWeek;

        // Verificar que el bloque existe y está activo
        var bloque = await _context.Disponibilidades
            .FirstOrDefaultAsync(d => d.DiaSemana == diaSemana && d.HoraInicio == hora && d.Activo);

        if (bloque == null) return false;

        // Contar citas ya agendadas en ese horario
        var citasOcupadas = await _context.Citas
            .CountAsync(c =>
                c.FechaCita == fecha &&
                c.HoraCita == hora &&
                c.Estado == EstadoCita.Confirmada);

        return citasOcupadas < bloque.MaxCitas;
    }

    /// <inheritdoc/>
    public async Task<string> GenerateFolioAsync(DateOnly fecha)
    {
        try
        {
            var prefijo = $"CITA-{fecha:yyyyMMdd}-";

            // Contar las citas ya registradas para esa fecha
            var count = await _context.Citas
                .CountAsync(c => c.FolioConfirmacion.StartsWith(prefijo));

            return $"{prefijo}{(count + 1):D4}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar folio para fecha {Fecha}", fecha);
            throw;
        }
    }
}
