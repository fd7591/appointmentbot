using botFacturacion.Application.Interfaces;
using botFacturacion.Domain.Entities;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace botFacturacion.Infrastructure.ExternalServices;

public class GoogleCalendarOptions
{
    public const string SectionName = "GoogleCalendar";

    /// <summary>Ruta al archivo JSON de la Service Account (puede reutilizar el mismo de Sheets).</summary>
    public string ServiceAccountPath { get; set; } = string.Empty;

    /// <summary>ID del calendario (email del calendario o "primary").</summary>
    public string CalendarId { get; set; } = string.Empty;

    /// <summary>Zona horaria IANA del consultorio.</summary>
    public string TimeZone { get; set; } = "America/Mexico_City";

    /// <summary>Hora de inicio de atención en formato HH:mm.</summary>
    public string HoraInicio { get; set; } = "09:00";

    /// <summary>Hora de fin de atención en formato HH:mm.</summary>
    public string HoraFin { get; set; } = "13:00";

    /// <summary>Duración de cada slot en minutos.</summary>
    public int SlotMinutos { get; set; } = 30;
}

public class GoogleCalendarService : IGoogleCalendarService
{
    private readonly GoogleCalendarOptions _options;
    private readonly string _serviceAccountPath;
    private readonly IHostEnvironment _env;
    private readonly ILogger<GoogleCalendarService> _logger;
    private CalendarService? _calendarService;

    public GoogleCalendarService(
        IOptions<GoogleCalendarOptions> options,
        IOptions<GoogleSheetsOptions> sheetsOptions,
        IHostEnvironment env,
        ILogger<GoogleCalendarService> logger)
    {
        _options = options.Value;
        _env = env;
        _logger = logger;

        // Usar la ruta del Calendar si está configurada, si no reutilizar la de Sheets
        _serviceAccountPath = !string.IsNullOrWhiteSpace(_options.ServiceAccountPath)
            ? _options.ServiceAccountPath
            : sheetsOptions.Value.ServiceAccountPath;
    }

    // ─── Slots disponibles ────────────────────────────────────────────────────

    public async Task<IReadOnlyList<string>> GetAvailableSlotsAsync(DateOnly fecha)
    {
        if (string.IsNullOrWhiteSpace(_options.CalendarId))
        {
            _logger.LogWarning("GoogleCalendar.CalendarId no está configurado.");
            return Array.Empty<string>();
        }

        if (fecha.DayOfWeek == DayOfWeek.Sunday)
            return Array.Empty<string>();

        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(ObtenerTzId());
            var horaInicio = TimeSpan.Parse(_options.HoraInicio);
            var horaFin = TimeSpan.Parse(_options.HoraFin);

            var inicioDelDia = TimeZoneInfo.ConvertTimeToUtc(
                fecha.ToDateTime(TimeOnly.FromTimeSpan(horaInicio)), tz);
            var finDelDia = TimeZoneInfo.ConvertTimeToUtc(
                fecha.ToDateTime(TimeOnly.FromTimeSpan(horaFin)), tz);

            var service = await GetServiceAsync();
            var fbRequest = new FreeBusyRequest
            {
                TimeMinDateTimeOffset = inicioDelDia,
                TimeMaxDateTimeOffset = finDelDia,
                TimeZone = _options.TimeZone,
                Items = [new FreeBusyRequestItem { Id = _options.CalendarId }]
            };

            var fbResponse = await service.Freebusy.Query(fbRequest).ExecuteAsync();

            var busyPeriods = fbResponse.Calendars.TryGetValue(_options.CalendarId, out var calInfo)
                ? calInfo.Busy ?? []
                : (IList<TimePeriod>)[];

            var ahora = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            var esHoy = fecha == DateOnly.FromDateTime(ahora);

            var slotsDisponibles = GenerarSlots(horaInicio, horaFin)
                .Where(slot =>
                {
                    if (esHoy)
                    {
                        var slotDt = fecha.ToDateTime(TimeOnly.FromTimeSpan(slot));
                        if (slotDt <= ahora.AddMinutes(30)) return false;
                    }

                    var slotUtc = TimeZoneInfo.ConvertTimeToUtc(
                        fecha.ToDateTime(TimeOnly.FromTimeSpan(slot)), tz);
                    var slotFinUtc = slotUtc.AddMinutes(_options.SlotMinutos);

                    return !busyPeriods.Any(b =>
                        b.StartDateTimeOffset < slotFinUtc && b.EndDateTimeOffset > slotUtc);
                })
                .Select(s => s.ToString(@"hh\:mm"))
                .ToList();

            _logger.LogInformation("{Count} slots disponibles en Calendar para {Fecha}", slotsDisponibles.Count, fecha);
            return slotsDisponibles;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener slots de Google Calendar para {Fecha}", fecha);
            return Array.Empty<string>();
        }
    }

    // ─── Crear evento ─────────────────────────────────────────────────────────

    public async Task CreateEventAsync(Cita cita)
    {
        if (string.IsNullOrWhiteSpace(_options.CalendarId)) return;

        try
        {
            var inicio = cita.FechaCita.ToDateTime(TimeOnly.FromTimeSpan(cita.HoraCita));
            var fin = inicio.AddMinutes(_options.SlotMinutos);

            var service = await GetServiceAsync();
            var evento = new Event
            {
                Summary = $"Cita: {cita.PacienteNombre}",
                Description =
                    $"Folio: {cita.FolioConfirmacion}\n" +
                    $"Tel: {cita.PacienteTelefonoContacto}\n" +
                    $"Email: {cita.PacienteEmail}\n" +
                    $"Motivo: {cita.Motivo}",
                Start = new EventDateTime { DateTimeRaw = inicio.ToString("yyyy-MM-ddTHH:mm:ss"), TimeZone = _options.TimeZone },
                End = new EventDateTime { DateTimeRaw = fin.ToString("yyyy-MM-ddTHH:mm:ss"), TimeZone = _options.TimeZone },
                ExtendedProperties = new Event.ExtendedPropertiesData
                {
                    Private__ = new Dictionary<string, string> { ["folio"] = cita.FolioConfirmacion }
                }
            };

            var creado = await service.Events.Insert(evento, _options.CalendarId).ExecuteAsync();
            _logger.LogInformation("Evento Calendar creado: {EventId} para folio {Folio}", creado.Id, cita.FolioConfirmacion);
        }
        catch (Exception ex)
        {
            // Best-effort: la cita ya está en BD, el calendario no bloquea la confirmación
            _logger.LogError(ex, "Error al crear evento en Calendar para folio {Folio}", cita.FolioConfirmacion);
        }
    }

    // ─── Cliente Google Calendar ──────────────────────────────────────────────

    private async Task<CalendarService> GetServiceAsync()
    {
        if (_calendarService != null) return _calendarService;

        GoogleCredential credential;
        var path = ResolverRutaServiceAccount();

        if (path != null)
        {
            _logger.LogInformation("Cargando Service Account (Calendar) desde: {Path}", path);
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
#pragma warning disable CS0618
            credential = GoogleCredential.FromStream(stream).CreateScoped(CalendarService.Scope.Calendar);
#pragma warning restore CS0618
        }
        else
        {
            _logger.LogWarning("Service Account no encontrada para Calendar. Usando ADC.");
            credential = (await GoogleCredential.GetApplicationDefaultAsync())
                .CreateScoped(CalendarService.Scope.Calendar);
        }

        _calendarService = new CalendarService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "BotFacturacion"
        });

        return _calendarService;
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private List<TimeSpan> GenerarSlots(TimeSpan inicio, TimeSpan fin)
    {
        var slots = new List<TimeSpan>();
        var actual = inicio;
        while (actual < fin)
        {
            slots.Add(actual);
            actual = actual.Add(TimeSpan.FromMinutes(_options.SlotMinutos));
        }
        return slots;
    }

    private string? ResolverRutaServiceAccount()
    {
        var configPath = _serviceAccountPath;
        if (string.IsNullOrWhiteSpace(configPath)) return null;

        if (File.Exists(configPath)) return Path.GetFullPath(configPath);

        var desdeContentRoot = Path.Combine(_env.ContentRootPath, configPath);
        if (File.Exists(desdeContentRoot)) return desdeContentRoot;

        var dir = new DirectoryInfo(_env.ContentRootPath);
        while (dir != null)
        {
            var candidato = Path.Combine(dir.FullName, configPath);
            if (File.Exists(candidato)) return candidato;
            dir = dir.Parent;
        }

        return null;
    }

    private string ObtenerTzId()
    {
        try { TimeZoneInfo.FindSystemTimeZoneById(_options.TimeZone); return _options.TimeZone; }
        catch { return "Central Standard Time"; }
    }
}
