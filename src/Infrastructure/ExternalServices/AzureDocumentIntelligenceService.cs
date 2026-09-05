using System.Text.RegularExpressions;
using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using botFacturacion.Application.Interfaces;
using botFacturacion.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace botFacturacion.Infrastructure.ExternalServices;

/// <summary>
/// Configuración para Azure Document Intelligence (ex Form Recognizer).
/// </summary>
public class AzureDocumentIntelligenceOptions
{
    public const string SectionName = "AzureDocumentIntelligence";

    /// <summary>Endpoint del recurso Azure, ej. "https://mi-recurso.cognitiveservices.azure.com/"</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>Clave de API del recurso Azure.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Modelo a usar. Default: "prebuilt-layout" (extrae texto y layout sin costo adicional).</summary>
    public string ModelId { get; set; } = "prebuilt-layout";
}

/// <summary>
/// Implementación de <see cref="IAzureDocumentIntelligenceService"/> usando
/// Azure Form Recognizer SDK (Azure.AI.FormRecognizer v4.x) con el modelo prebuilt-layout.
///
/// Estrategia de extracción para la Constancia de Situación Fiscal del SAT:
/// 1. Se extrae el texto completo del documento mediante OCR.
/// 2. Se aplican expresiones regulares optimizadas para el formato del documento SAT.
/// 3. Para el régimen fiscal se busca primero el código numérico; si no aparece,
///    se intenta emparejar la descripción textual con el catálogo conocido.
/// </summary>
public class AzureDocumentIntelligenceService : IAzureDocumentIntelligenceService
{
    private readonly DocumentAnalysisClient _client;
    private readonly string _modelId;
    private readonly ILogger<AzureDocumentIntelligenceService> _logger;

    // ── Patrones para la Constancia de Situación Fiscal ──────────────────────

    // RFC en cualquier lugar del texto (incluye variantes con/sin dos puntos y espacios)
    private static readonly Regex _rfcRegex = new(
        @"RFC\s*:?\s*([A-ZÑ&]{3,4}\d{6}[A-Z\d]{3})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Código Postal en la sección de domicilio fiscal
    private static readonly Regex _cpRegex = new(
        @"C[oó]digo\s+Postal\s*:?\s*(\d{5})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Razón social para persona moral
    private static readonly Regex _razonSocialRegex = new(
        @"Denominaci[oó]n\s*/\s*Raz[oó]n\s+Social\s*:?\s*([^\n\r]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Partes del nombre para persona física
    private static readonly Regex _nombresRegex = new(
        @"Nombre\(s\)\s*:?\s*([^\n\r]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex _apellido1Regex = new(
        @"Primer\s+Apellido\s*:?\s*([^\n\r]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex _apellido2Regex = new(
        @"Segundo\s+Apellido\s*:?\s*([^\n\r]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Código numérico de régimen (3 dígitos en rango SAT 601-699)
    private static readonly Regex _regimenCodigoRegex = new(
        @"\b(6\d{2})\b",
        RegexOptions.Compiled);

    public AzureDocumentIntelligenceService(
        IOptions<AzureDocumentIntelligenceOptions> options,
        ILogger<AzureDocumentIntelligenceService> logger)
    {
        var opts = options.Value;
        _client = new DocumentAnalysisClient(
            new Uri(opts.Endpoint),
            new AzureKeyCredential(opts.ApiKey));
        _modelId = opts.ModelId;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ConstanciaFiscalDto> AnalyzeConstanciaAsync(Stream documentStream, string? mimeType = null)
    {
        var resultado = new ConstanciaFiscalDto();

        try
        {
            _logger.LogInformation("Iniciando análisis de Constancia de Situación Fiscal con modelo '{Model}'", _modelId);

            var operation = await _client.AnalyzeDocumentAsync(
                WaitUntil.Completed,
                _modelId,
                documentStream);

            var analyzeResult = operation.Value;
            var textoCompleto = analyzeResult.Content ?? string.Empty;

            _logger.LogDebug("Texto extraído ({Chars} chars). Primeros 300: {Snippet}",
                textoCompleto.Length,
                textoCompleto[..Math.Min(300, textoCompleto.Length)]);

            // ── Extraer RFC ──────────────────────────────────────────────────
            var rfcMatch = _rfcRegex.Match(textoCompleto);
            if (rfcMatch.Success)
                resultado.RFC = rfcMatch.Groups[1].Value.ToUpperInvariant().Trim();

            // ── Extraer Código Postal ────────────────────────────────────────
            // Puede haber más de uno en el documento; tomamos el primero en la
            // sección de Domicilio Fiscal (que suele aparecer después de "RFC:")
            var cpMatch = _cpRegex.Match(textoCompleto, rfcMatch.Success ? rfcMatch.Index : 0);
            if (cpMatch.Success)
                resultado.CodigoPostal = cpMatch.Groups[1].Value.Trim();

            // ── Extraer Razón Social ─────────────────────────────────────────
            var razonSocialMatch = _razonSocialRegex.Match(textoCompleto);
            if (razonSocialMatch.Success)
            {
                resultado.RazonSocial = LimpiarTexto(razonSocialMatch.Groups[1].Value);
            }
            else
            {
                // Intentar construir nombre de persona física: APELLIDO1 APELLIDO2 NOMBRE(S)
                var ap1 = _apellido1Regex.Match(textoCompleto);
                var ap2 = _apellido2Regex.Match(textoCompleto);
                var nombres = _nombresRegex.Match(textoCompleto);

                if (ap1.Success)
                {
                    var partes = new List<string> { LimpiarTexto(ap1.Groups[1].Value) };
                    if (ap2.Success && !string.IsNullOrWhiteSpace(ap2.Groups[1].Value))
                        partes.Add(LimpiarTexto(ap2.Groups[1].Value));
                    if (nombres.Success && !string.IsNullOrWhiteSpace(nombres.Groups[1].Value))
                        partes.Add(LimpiarTexto(nombres.Groups[1].Value));

                    resultado.RazonSocial = string.Join(" ", partes.Where(p => !string.IsNullOrEmpty(p)));
                }
            }

            // ── Extraer Régimen Fiscal ───────────────────────────────────────
            // Primero buscar código numérico explícito (algunas versiones del SAT lo incluyen)
            var regimenCodigoMatches = _regimenCodigoRegex.Matches(textoCompleto);
            foreach (Match match in regimenCodigoMatches)
            {
                var clave = match.Groups[1].Value;
                var regimen = RegimenFiscalExtensions.FromClave(clave);
                if (regimen != null)
                {
                    resultado.RegimenFiscalClave = regimen.Value.GetClave();
                    resultado.RegimenFiscalDescripcion = regimen.Value.GetDescripcion();
                    break;
                }
            }

            // Si no se encontró código, intentar por descripción textual
            if (resultado.RegimenFiscalClave == null)
            {
                var regimenPorDescripcion = MatchRegimenPorDescripcion(textoCompleto);
                if (regimenPorDescripcion != null)
                {
                    resultado.RegimenFiscalClave = regimenPorDescripcion.Value.GetClave();
                    resultado.RegimenFiscalDescripcion = regimenPorDescripcion.Value.GetDescripcion();
                }
            }

            // ── Calcular confianza ───────────────────────────────────────────
            int camposExtraidos = 0;
            if (!string.IsNullOrEmpty(resultado.RFC)) camposExtraidos++;
            if (!string.IsNullOrEmpty(resultado.RazonSocial)) camposExtraidos++;
            if (!string.IsNullOrEmpty(resultado.RegimenFiscalClave)) camposExtraidos++;
            if (!string.IsNullOrEmpty(resultado.CodigoPostal)) camposExtraidos++;

            resultado.Confianza = camposExtraidos / 4f;
            resultado.Exitoso = camposExtraidos >= 2; // Mínimo RFC + un campo más

            if (!resultado.Exitoso)
                resultado.ErrorMensaje = "No se pudieron extraer suficientes datos del documento. " +
                    "Verifica que sea una Constancia de Situación Fiscal válida del SAT.";

            _logger.LogInformation(
                "Constancia analizada: RFC={RFC}, RazonSocial={RS}, Regimen={Reg}, CP={CP}, Confianza={Conf:P0}, Exitoso={Ok}",
                resultado.RFC, resultado.RazonSocial, resultado.RegimenFiscalClave,
                resultado.CodigoPostal, resultado.Confianza, resultado.Exitoso);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al analizar constancia con Azure Document Intelligence");
            resultado.Exitoso = false;
            resultado.ErrorMensaje = "No se pudo procesar el documento. Por favor ingresa los datos manualmente.";
        }

        return resultado;
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>Limpia y normaliza un texto extraído por OCR (elimina espacios extra, convierte a mayúsculas).</summary>
    private static string LimpiarTexto(string texto) =>
        Regex.Replace(texto.Trim(), @"\s{2,}", " ").ToUpperInvariant();

    /// <summary>
    /// Intenta emparejar el régimen fiscal a partir de palabras clave en el texto completo del documento.
    /// Útil cuando el SAT no incluye el código numérico en la constancia.
    /// </summary>
    private static RegimenFiscal? MatchRegimenPorDescripcion(string texto)
    {
        var t = texto.ToLowerInvariant();

        // Orden de mayor a menor especificidad para evitar falsos positivos
        if (t.Contains("simplificado de confianza") && (t.Contains("morales") || t.Contains("moral")))
            return RegimenFiscal.RescoPersonasMorales;

        if (t.Contains("simplificado de confianza"))
            return RegimenFiscal.RescoPersonasFisicas;

        if (t.Contains("actividades empresariales") && t.Contains("profesionales"))
            return RegimenFiscal.ActividadesEmpresarialesProfesionales;

        if (t.Contains("plataformas tecnol"))
            return RegimenFiscal.PlataformasTecnologicas;

        if (t.Contains("incorporaci") && t.Contains("fiscal"))
            return RegimenFiscal.IncorporacionFiscal;

        if (t.Contains("sueldos y salarios") || t.Contains("asimilados a salarios"))
            return RegimenFiscal.SueldosYSalarios;

        if (t.Contains("arrendamiento"))
            return RegimenFiscal.Arrendamiento;

        if (t.Contains("dividendos") && t.Contains("socios"))
            return RegimenFiscal.IngresosDividendos;

        if (t.Contains("general de ley") && t.Contains("morales"))
            return RegimenFiscal.GeneralPersonasMorales;

        if (t.Contains("fines no lucrativos") || t.Contains("sin fines de lucro"))
            return RegimenFiscal.PersonasMoralesFinesNoLucrativos;

        if (t.Contains("agr") && (t.Contains("ganaderas") || t.Contains("silv") || t.Contains("pesqueras")))
            return RegimenFiscal.ActividadesAgricolasPesqueras;

        if (t.Contains("enajenaci") || t.Contains("adquisici") && t.Contains("bienes"))
            return RegimenFiscal.EnajenacionAdquisicionBienes;

        if (t.Contains("dem") && t.Contains("ingresos"))
            return RegimenFiscal.DemasIngresos;

        return null;
    }
}
