using botFacturacion.Application.Interfaces;
using botFacturacion.Domain.Entities;
using botFacturacion.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace botFacturacion.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementación del repositorio de estado de conversación usando Entity Framework Core.
/// </summary>
public class ConversationRepository : IConversationRepository
{
    private readonly AppDbContext _context;
    private readonly ILogger<ConversationRepository> _logger;

    public ConversationRepository(AppDbContext context, ILogger<ConversationRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ConversationState> GetOrCreateAsync(string phoneNumber)
    {
        try
        {
            var estado = await _context.ConversationStates
                .FirstOrDefaultAsync(c => c.PhoneNumber == phoneNumber);

            if (estado == null)
            {
                estado = new ConversationState
                {
                    PhoneNumber = phoneNumber,
                    CurrentFlow = FlowType.None,
                    CurrentStep = StepType.None,
                    TempData = null,
                    UpdatedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                };
                _context.ConversationStates.Add(estado);
                await _context.SaveChangesAsync();
                _logger.LogDebug("Estado de conversación creado para {Phone}", phoneNumber);
            }

            return estado;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener/crear estado de conversación para {Phone}", phoneNumber);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(string phoneNumber, FlowType flow, StepType step, string? tempData = null)
    {
        try
        {
            var estado = await _context.ConversationStates
                .FirstOrDefaultAsync(c => c.PhoneNumber == phoneNumber);

            if (estado == null)
            {
                estado = new ConversationState { PhoneNumber = phoneNumber, CreatedAt = DateTime.UtcNow };
                _context.ConversationStates.Add(estado);
            }

            estado.CurrentFlow = flow;
            estado.CurrentStep = step;
            estado.TempData = tempData;
            estado.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al actualizar estado de conversación para {Phone}", phoneNumber);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task ResetAsync(string phoneNumber)
    {
        try
        {
            var estado = await _context.ConversationStates
                .FirstOrDefaultAsync(c => c.PhoneNumber == phoneNumber);

            if (estado != null)
            {
                estado.CurrentFlow = FlowType.None;
                estado.CurrentStep = StepType.None;
                estado.TempData = null;
                estado.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al reiniciar estado de conversación para {Phone}", phoneNumber);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<ConversationState>> GetExpiredSessionsAsync(DateTime threshold)
    {
        try
        {
            return await _context.ConversationStates
                .Where(c => c.CurrentFlow != FlowType.None && c.UpdatedAt < threshold)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener sesiones expiradas");
            throw;
        }
    }
}
