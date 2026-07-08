using System.Text.Json;
using SanalPOS.Application.Common.Exceptions;
using SanalPOS.Domain.Exceptions;

namespace SanalPOS.API.Middleware;

/// <summary>Tüm hataları RFC 7807 Problem Details formatına çevirir (bkz. docs/04-validasyon.md §6).</summary>
public class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (status, type, title, errors) = exception switch
        {
            SanalPosValidationException validationEx =>
                (StatusCodes.Status400BadRequest, "https://sanalpos.com/errors/validation", "Validasyon hatası",
                    (object?)validationEx.Errors),
            NotFoundException =>
                (StatusCodes.Status404NotFound, "https://sanalpos.com/errors/not-found", exception.Message, null),
            ConflictException =>
                (StatusCodes.Status409Conflict, "https://sanalpos.com/errors/conflict", exception.Message, null),
            UnauthorizedException =>
                (StatusCodes.Status401Unauthorized, "https://sanalpos.com/errors/unauthorized", exception.Message, null),
            ForbiddenException =>
                (StatusCodes.Status403Forbidden, "https://sanalpos.com/errors/forbidden", exception.Message, null),
            DomainException =>
                (StatusCodes.Status422UnprocessableEntity, "https://sanalpos.com/errors/business-rule", exception.Message, null),
            _ =>
                (StatusCodes.Status500InternalServerError, "https://sanalpos.com/errors/internal",
                    "Beklenmeyen bir hata oluştu.", null)
        };

        if (status >= 500)
            _logger.LogError(exception, "İşlenmemiş hata: {Message}", exception.Message);
        else
            _logger.LogWarning("İstek hatası ({StatusCode}): {Message}", status, exception.Message);

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";

        var problem = new Dictionary<string, object?>
        {
            ["type"] = type,
            ["title"] = title,
            ["status"] = status,
            ["traceId"] = context.TraceIdentifier
        };
        if (errors is not null)
            problem["errors"] = errors;

        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
    }
}
