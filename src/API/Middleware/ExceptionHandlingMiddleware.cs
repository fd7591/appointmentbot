using System.Net;
using System.Text.Json;

namespace botFacturacion.API.Middleware;

/// <summary>
/// Middleware global de manejo de excepciones no capturadas.
/// Registra el error y retorna una respuesta JSON estándar.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Excepción no controlada en {Path}", context.Request.Path);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var (statusCode, mensaje) = exception switch
        {
            ArgumentException => (HttpStatusCode.BadRequest, "Solicitud inválida."),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "No autorizado."),
            _ => (HttpStatusCode.InternalServerError, "Ocurrió un error interno.")
        };

        context.Response.StatusCode = (int)statusCode;

        var response = new
        {
            error = mensaje,
            status = (int)statusCode
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}

/// <summary>
/// Método de extensión para registrar el middleware de forma fluida.
/// </summary>
public static class ExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseExceptionHandling(this IApplicationBuilder app)
        => app.UseMiddleware<ExceptionHandlingMiddleware>();
}
