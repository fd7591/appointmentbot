using botFacturacion.Application.DTOs;
using botFacturacion.Application.Interfaces;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace botFacturacion.Infrastructure.ExternalServices;

/// <summary>
/// Configuración para la integración con Google Sheets.
/// </summary>
public class GoogleSheetsOptions
{
    public const string SectionName = "GoogleSheets";

    /// <summary>Ruta al archivo JSON de la Service Account de Google Cloud.</summary>
    public string ServiceAccountPath { get; set; } = string.Empty;

    /// <summary>ID del spreadsheet de Google Sheets (extraído de la URL).</summary>
    public string SpreadsheetId { get; set; } = string.Empty;

    /// <summary>Nombre de la hoja donde se registran las solicitudes.</summary>
    public string SheetName { get; set; } = "Solicitudes";
}

/// <summary>
/// Implementación del servicio de Google Sheets usando la biblioteca oficial de Google.
/// Usa autenticación mediante Service Account (JSON key file).
/// </summary>
public class GoogleSheetsService : IGoogleSheetsService
{
    private readonly GoogleSheetsOptions _options;
    private readonly IHostEnvironment _env;
    private readonly ILogger<GoogleSheetsService> _logger;
    private SheetsService? _sheetsService;

    public GoogleSheetsService(
        IOptions<GoogleSheetsOptions> options,
        IHostEnvironment env,
        ILogger<GoogleSheetsService> logger)
    {
        _options = options.Value;
        _env = env;
        _logger = logger;
    }

    // ─── Inicialización del cliente ────────────────────────────────────────────

    /// <summary>
    /// Obtiene (o crea) la instancia del cliente de Google Sheets.
    /// La autenticación se realiza con la Service Account configurada en appsettings.
    /// </summary>
    private async Task<SheetsService> GetServiceAsync()
    {
        if (_sheetsService != null) return _sheetsService;

        try
        {
            GoogleCredential credential;

            var resolvedPath = ResolverRutaServiceAccount();
            if (resolvedPath != null)
            {
                _logger.LogInformation("Cargando Service Account desde: {Path}", resolvedPath);
                using var stream = new FileStream(resolvedPath, FileMode.Open, FileAccess.Read);
                credential = GoogleCredential
                    .FromStream(stream)
                    .CreateScoped(SheetsService.Scope.Spreadsheets);
            }
            else
            {
                _logger.LogWarning(
                    "Archivo de Service Account no encontrado en ninguna ruta conocida (configurado: '{Path}'). Intentando Application Default Credentials.",
                    _options.ServiceAccountPath);
                credential = await GoogleCredential.GetApplicationDefaultAsync();
                credential = credential.CreateScoped(SheetsService.Scope.Spreadsheets);
            }

            _sheetsService = new SheetsService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "BotFacturacion"
            });

            // Diagnóstico: listar hojas disponibles en el spreadsheet
            try
            {
                var meta = await _sheetsService.Spreadsheets.Get(_options.SpreadsheetId).ExecuteAsync();
                var hojas = meta.Sheets?.Select(s => s.Properties.Title).ToList() ?? [];
                _logger.LogInformation("Spreadsheet '{Title}' — hojas disponibles: [{Hojas}]",
                    meta.Properties?.Title, string.Join(", ", hojas));
            }
            catch (Exception diagEx)
            {
                _logger.LogWarning("No se pudo leer el spreadsheet (ID: {Id}): {Msg}",
                    _options.SpreadsheetId, diagEx.Message);
            }

            return _sheetsService;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al inicializar el cliente de Google Sheets");
            throw;
        }
    }

    // ─── Resolución de ruta del archivo de credenciales ──────────────────────

    /// <summary>
    /// Busca el archivo de Service Account en varias rutas posibles:
    /// tal cual está en config, relativo al ContentRoot, o relativo a la raíz del proyecto.
    /// </summary>
    private string? ResolverRutaServiceAccount()
    {
        var configPath = _options.ServiceAccountPath;

        // 1. Ruta absoluta o relativa al directorio de trabajo actual
        if (File.Exists(configPath))
            return Path.GetFullPath(configPath);

        // 2. Relativo al ContentRoot (ej. src/API)
        var desdeContentRoot = Path.Combine(_env.ContentRootPath, configPath);
        if (File.Exists(desdeContentRoot))
            return desdeContentRoot;

        // 3. Subir niveles desde ContentRoot buscando la raíz del proyecto
        var directorio = new DirectoryInfo(_env.ContentRootPath);
        while (directorio != null)
        {
            var candidato = Path.Combine(directorio.FullName, configPath);
            if (File.Exists(candidato))
                return candidato;
            directorio = directorio.Parent;
        }

        return null;
    }

    // ─── Agregar fila ──────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<int> AppendRowAsync(GoogleSheetsRowDto row)
    {
        try
        {
            var service = await GetServiceAsync();
            var range = $"'{_options.SheetName}'!A1";

            var valueRange = new ValueRange
            {
                Values = new List<IList<object>> { row.ToRowValues() }
            };

            var request = service.Spreadsheets.Values.Append(
                valueRange,
                _options.SpreadsheetId,
                range);

            request.ValueInputOption =
                SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;
            request.InsertDataOption =
                SpreadsheetsResource.ValuesResource.AppendRequest.InsertDataOptionEnum.INSERTROWS;

            var response = await request.ExecuteAsync();

            // Extraer el número de fila insertada del rango de la respuesta
            // El rango de actualización tiene formato "Solicitudes!A15:S15"
            var updatedRange = response.Updates?.UpdatedRange ?? string.Empty;
            var filaInsertada = ExtraerNumeroFila(updatedRange);

            _logger.LogInformation(
                "Fila {Fila} agregada en Google Sheets para folio {Folio}",
                filaInsertada, row.Folio);

            return filaInsertada;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al agregar fila en Google Sheets para folio {Folio}", row.Folio);
            throw;
        }
    }

    // ─── Actualizar estado ─────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task UpdateEstadoAsync(int rowNumber, string estado)
    {
        try
        {
            var service = await GetServiceAsync();
            var range = $"'{_options.SheetName}'!Q{rowNumber}";

            var valueRange = new ValueRange
            {
                Values = new List<IList<object>> { new List<object> { estado } }
            };

            var request = service.Spreadsheets.Values.Update(
                valueRange,
                _options.SpreadsheetId,
                range);

            request.ValueInputOption =
                SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;

            await request.ExecuteAsync();
            _logger.LogInformation("Estado actualizado a '{Estado}' en fila {Fila}", estado, rowNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al actualizar estado en fila {Fila}", rowNumber);
            throw;
        }
    }

    // ─── Actualizar folio fiscal ───────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task UpdateFolioFiscalAsync(int rowNumber, string notas, string folioFiscalUuid)
    {
        try
        {
            var service = await GetServiceAsync();
            // Columnas R (notas) y S (UUID) — rango de 2 columnas
            var range = $"'{_options.SheetName}'!R{rowNumber}:S{rowNumber}";

            var valueRange = new ValueRange
            {
                Values = new List<IList<object>>
                {
                    new List<object> { notas, folioFiscalUuid }
                }
            };

            var request = service.Spreadsheets.Values.Update(
                valueRange,
                _options.SpreadsheetId,
                range);

            request.ValueInputOption =
                SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;

            await request.ExecuteAsync();
            _logger.LogInformation("Folio fiscal UUID '{UUID}' guardado en fila {Fila}", folioFiscalUuid, rowNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al actualizar folio fiscal en fila {Fila}", rowNumber);
            throw;
        }
    }

    // ─── Test de conexión ──────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            var service = await GetServiceAsync();
            var request = service.Spreadsheets.Get(_options.SpreadsheetId);
            var spreadsheet = await request.ExecuteAsync();
            _logger.LogInformation("Conexión a Google Sheets OK: '{Title}'", spreadsheet.Properties.Title);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló la prueba de conexión a Google Sheets");
            return false;
        }
    }

    // ─── Helper ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Extrae el número de fila de un rango de Google Sheets.
    /// Ejemplo: "Solicitudes!A15:S15" → 15
    /// </summary>
    private static int ExtraerNumeroFila(string range)
    {
        try
        {
            // Formato: "Hoja!A{fila}:Z{fila}"
            var parte = range.Split('!').LastOrDefault() ?? string.Empty;
            var match = System.Text.RegularExpressions.Regex.Match(parte, @"[A-Z]+(\d+)");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var fila))
                return fila;
        }
        catch { /* ignorar */ }

        return 0;
    }
}
