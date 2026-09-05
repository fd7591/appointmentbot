using botFacturacion.Application.Interfaces;
using botFacturacion.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace botFacturacion.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementación del repositorio de facturas y receptores fiscales usando Entity Framework Core.
/// </summary>
public class FacturaRepository : IFacturaRepository
{
    private readonly AppDbContext _context;
    private readonly ILogger<FacturaRepository> _logger;

    public FacturaRepository(AppDbContext context, ILogger<FacturaRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    // ─── ReceptorFiscal ───────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<ReceptorFiscal?> GetReceptorByRFCAsync(string rfc)
    {
        return await _context.ReceptoresFiscales
            .FirstOrDefaultAsync(r => r.RFC == rfc.ToUpperInvariant());
    }

    /// <inheritdoc/>
    public async Task<ReceptorFiscal> CreateReceptorAsync(ReceptorFiscal receptor)
    {
        try
        {
            _context.ReceptoresFiscales.Add(receptor);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Receptor fiscal creado: {RFC}", receptor.RFC);
            return receptor;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear receptor fiscal {RFC}", receptor.RFC);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<ReceptorFiscal> UpdateReceptorAsync(ReceptorFiscal receptor)
    {
        try
        {
            _context.ReceptoresFiscales.Update(receptor);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Receptor fiscal actualizado: {RFC}", receptor.RFC);
            return receptor;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al actualizar receptor fiscal {RFC}", receptor.RFC);
            throw;
        }
    }

    // ─── SolicitudFactura ─────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<SolicitudFactura> CreateSolicitudAsync(SolicitudFactura solicitud)
    {
        try
        {
            _context.SolicitudesFactura.Add(solicitud);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Solicitud de factura creada: {Folio}", solicitud.Folio);
            return solicitud;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear solicitud de factura {Folio}", solicitud.Folio);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<SolicitudFactura?> GetByFolioAsync(string folio)
    {
        return await _context.SolicitudesFactura
            .Include(s => s.ReceptorFiscal)
            .FirstOrDefaultAsync(s => s.Folio == folio);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<SolicitudFactura>> GetByPhoneAsync(string telefono)
    {
        return await _context.SolicitudesFactura
            .Include(s => s.ReceptorFiscal)
            .Where(s => s.TelefonoSolicitante == telefono)
            .OrderByDescending(s => s.CreadoEn)
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<string> GenerateFolioAsync()
    {
        try
        {
            var hoy = DateTime.Now;
            var prefijo = $"FACT-{hoy:yyyyMMdd}-";

            var count = await _context.SolicitudesFactura
                .CountAsync(s => s.Folio.StartsWith(prefijo));

            return $"{prefijo}{(count + 1):D4}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar folio de factura");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task UpdateFilaGoogleSheetsAsync(int solicitudId, int fila)
    {
        try
        {
            var solicitud = await _context.SolicitudesFactura.FindAsync(solicitudId);
            if (solicitud != null)
            {
                solicitud.FilaGoogleSheets = fila;
                solicitud.ActualizadoEn = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al actualizar fila de Google Sheets para solicitud {Id}", solicitudId);
            // No lanzar excepción — este error no debe interrumpir el flujo principal
        }
    }
}
