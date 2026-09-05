using botFacturacion.Application.Interfaces;
using botFacturacion.Application.Services;
using botFacturacion.Infrastructure.BackgroundServices;
using botFacturacion.Infrastructure.ExternalServices;
using Microsoft.Extensions.Options;
using botFacturacion.Infrastructure.Persistence;
using botFacturacion.Infrastructure.Persistence.Repositories;
using botFacturacion.API.Middleware;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ─── Configuración ─────────────────────────────────────────────────────────────

builder.Services.AddOptions<WhatsAppOptions>()
    .Bind(builder.Configuration.GetSection(WhatsAppOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<GoogleSheetsOptions>()
    .Bind(builder.Configuration.GetSection(GoogleSheetsOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<AppOptions>()
    .Bind(builder.Configuration.GetSection(AppOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<AzureDocumentIntelligenceOptions>()
    .Bind(builder.Configuration.GetSection(AzureDocumentIntelligenceOptions.SectionName));
    // No ValidateOnStart para no bloquear el arranque si no está configurado aún

// ─── Base de datos (SQL Server + Entity Framework Core) ─────────────────────────

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlServerOptions =>
        {
            sqlServerOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorNumbersToAdd: null);
        }));

// ─── Repositorios (Infrastructure) ────────────────────────────────────────────

builder.Services.AddScoped<IConversationRepository, ConversationRepository>();
builder.Services.AddScoped<ICitaRepository, CitaRepository>();
builder.Services.AddScoped<IFacturaRepository, FacturaRepository>();

// ─── Servicios externos ────────────────────────────────────────────────────────

// Cliente HTTP para WhatsApp Cloud API con política de reintentos
builder.Services.AddHttpClient<IWhatsAppService, WhatsAppService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
}).SetHandlerLifetime(TimeSpan.FromMinutes(5));

builder.Services.AddScoped<IGoogleSheetsService, GoogleSheetsService>();
builder.Services.AddScoped<IAzureDocumentIntelligenceService, AzureDocumentIntelligenceService>();
builder.Services.AddScoped<IGoogleCalendarService, GoogleCalendarService>();

builder.Services.AddOptions<GoogleCalendarOptions>()
    .Bind(builder.Configuration.GetSection(GoogleCalendarOptions.SectionName));

// ─── Servicios de aplicación ───────────────────────────────────────────────────

builder.Services.AddScoped<CitaService>(sp =>
    new CitaService(
        sp.GetRequiredService<IConversationRepository>(),
        sp.GetRequiredService<ICitaRepository>(),
        sp.GetRequiredService<IWhatsAppService>(),
        sp.GetRequiredService<IGoogleCalendarService>(),
        sp.GetRequiredService<ILogger<CitaService>>()));

builder.Services.AddScoped<FacturaService>(sp =>
{
    var appOptions = builder.Configuration
        .GetSection(AppOptions.SectionName)
        .Get<AppOptions>() ?? new AppOptions();

    return new FacturaService(
        sp.GetRequiredService<IConversationRepository>(),
        sp.GetRequiredService<IFacturaRepository>(),
        sp.GetRequiredService<IGoogleSheetsService>(),
        sp.GetRequiredService<IWhatsAppService>(),
        sp.GetRequiredService<IAzureDocumentIntelligenceService>(),
        appOptions.FacturacionSoloMesActual,
        sp.GetRequiredService<ILogger<FacturaService>>());
});

builder.Services.AddScoped<ConversationService>(sp =>
{
    var appOptions = builder.Configuration
        .GetSection(AppOptions.SectionName)
        .Get<AppOptions>() ?? new AppOptions();

    return new ConversationService(
        sp.GetRequiredService<IConversationRepository>(),
        sp.GetRequiredService<IWhatsAppService>(),
        sp.GetRequiredService<CitaService>(),
        sp.GetRequiredService<FacturaService>(),
        sp.GetRequiredService<ILogger<ConversationService>>(),
        appOptions.SessionTimeoutMinutes);
});

// ─── Servicio en segundo plano (recordatorios + limpieza) ─────────────────────

builder.Services.AddHostedService<ReminderService>();

// ─── ASP.NET Core ─────────────────────────────────────────────────────────────

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Mantener nombres de propiedades en camelCase para el JSON del webhook
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Bot Facturación API",
        Version = "v1",
        Description = "Bot de WhatsApp para agendar citas y solicitar facturas. " +
                      "Expone únicamente el endpoint del webhook de Meta."
    });
});

// ─── Logging ───────────────────────────────────────────────────────────────────

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// ─── Build ─────────────────────────────────────────────────────────────────────

var app = builder.Build();

// ─── Middleware pipeline ───────────────────────────────────────────────────────

app.UseExceptionHandling(); // Middleware global de excepciones

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Bot Facturación API v1");
        c.RoutePrefix = string.Empty; // Swagger en la raíz
    });
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// ─── Migraciones automáticas en desarrollo ─────────────────────────────────────

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        context.Database.Migrate();
        app.Logger.LogInformation("Migraciones aplicadas correctamente.");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Error al aplicar migraciones. Verifica la cadena de conexión.");
    }
}

app.Logger.LogInformation("Bot Facturación iniciado. Escuchando en {Urls}", string.Join(", ", app.Urls));

app.Run();
