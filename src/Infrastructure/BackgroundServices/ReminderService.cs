using botFacturacion.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace botFacturacion.Infrastructure.BackgroundServices;

/// <summary>
/// Opciones de configuración para el servicio de recordatorios.
/// </summary>
public class AppOptions
{
    public const string SectionName = "App";

    /// <summary>Minutos de inactividad antes de considerar una sesión expirada.</summary>
    public int SessionTimeoutMinutes { get; set; } = 30;

    /// <summary>Horas antes de la cita en que se envía el recordatorio automático.</summary>
    public int ReminderHoursBeforeAppointment { get; set; } = 24;

    /// <summary>
    /// Si true, solo se permiten facturar consultas del mes calendario actual.
    /// Si false, se aplica la regla general de hasta 2 años atrás según el SAT.
    /// </summary>
    public bool FacturacionSoloMesActual { get; set; } = true;
}

/// <summary>
/// Servicio en segundo plano (Hosted Service) que ejecuta dos tareas periódicas:
///
/// 1. Recordatorios de citas: envía un mensaje de WhatsApp a pacientes con cita
///    al día siguiente (configurable en appsettings).
///
/// 2. Limpieza de sesiones: reinicia estados de conversación que superaron el
///    tiempo de inactividad configurado.
///
/// Ambas tareas se ejecutan cada 30 minutos de forma independiente.
/// </summary>
public class ReminderService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ReminderService> _logger;
    private readonly AppOptions _appOptions;

    // Intervalo de ejecución del loop principal
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(30);

    public ReminderService(
        IServiceProvider serviceProvider,
        IOptions<AppOptions> appOptions,
        ILogger<ReminderService> logger)
    {
        _serviceProvider = serviceProvider;
        _appOptions = appOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ReminderService iniciado. Intervalo: {Intervalo} minutos.",
            _checkInterval.TotalMinutes);

        // Esperar 1 minuto al arrancar para que la app esté completamente lista
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EnviarRecordatoriosCitasAsync(stoppingToken);
                await LimpiarSesionesExpiradasAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error durante la ejecución del ReminderService");
            }

            // Esperar hasta la próxima ejecución
            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("ReminderService detenido.");
    }

    // ─── Tarea 1: Recordatorios de citas ──────────────────────────────────────

    private async Task EnviarRecordatoriosCitasAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var citaRepo = scope.ServiceProvider.GetRequiredService<ICitaRepository>();
        var whatsApp = scope.ServiceProvider.GetRequiredService<IWhatsAppService>();

        try
        {
            // Calcular la fecha objetivo: hoy + horas configuradas
            var fechaObjetivo = DateOnly.FromDateTime(
                DateTime.Today.AddHours(_appOptions.ReminderHoursBeforeAppointment));

            var citasPendientes = await citaRepo.GetPendingRemindersAsync(fechaObjetivo);
            var enviados = 0;

            foreach (var cita in citasPendientes)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    var mensaje =
                        $"⏰ *Recordatorio de cita*\n\n" +
                        $"Hola *{cita.PacienteNombre}*, te recordamos que tienes una cita programada:\n\n" +
                        $"📅 Fecha: *{cita.FechaCita:dd/MM/yyyy}*\n" +
                        $"🕐 Hora: *{cita.HoraCita:hh\\:mm}*\n" +
                        $"📋 Folio: {cita.FolioConfirmacion}\n\n" +
                        $"Si necesitas cancelar o reprogramar, por favor comunícate al consultorio.\n\n" +
                        $"¡Te esperamos! 😊";

                    await whatsApp.SendTextAsync(cita.PacienteTelefono, mensaje);
                    await citaRepo.MarkReminderSentAsync(cita.Id);
                    enviados++;

                    _logger.LogInformation(
                        "Recordatorio enviado para cita {Folio} al teléfono {Phone}",
                        cita.FolioConfirmacion, cita.PacienteTelefono);

                    // Pequeña pausa entre mensajes para no saturar la API
                    await Task.Delay(500, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Error al enviar recordatorio para cita {Folio}",
                        cita.FolioConfirmacion);
                    // Continuar con la siguiente cita
                }
            }

            if (enviados > 0)
                _logger.LogInformation("Recordatorios enviados: {Count}", enviados);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en el proceso de recordatorios de citas");
        }
    }

    // ─── Tarea 2: Limpieza de sesiones expiradas ──────────────────────────────

    private async Task LimpiarSesionesExpiradasAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var conversationRepo = scope.ServiceProvider.GetRequiredService<IConversationRepository>();

        try
        {
            var umbral = DateTime.UtcNow.AddMinutes(-_appOptions.SessionTimeoutMinutes);
            var sesionesExpiradas = await conversationRepo.GetExpiredSessionsAsync(umbral);

            var limpiezas = 0;
            foreach (var sesion in sesionesExpiradas)
            {
                if (ct.IsCancellationRequested) break;

                await conversationRepo.ResetAsync(sesion.PhoneNumber);
                limpiezas++;
            }

            if (limpiezas > 0)
                _logger.LogInformation("Sesiones expiradas limpiadas: {Count}", limpiezas);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al limpiar sesiones expiradas");
        }
    }
}
