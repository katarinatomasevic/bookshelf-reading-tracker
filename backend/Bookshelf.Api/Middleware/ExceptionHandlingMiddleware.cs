using System.Net;
using Bookshelf.Application.Common.Exceptions;

namespace Bookshelf.Api.Middleware;

public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            var statusCode = exception switch
            {
                NotFoundException => HttpStatusCode.NotFound,
                ForbiddenException => HttpStatusCode.Forbidden,
                ValidationException => HttpStatusCode.BadRequest,
                UpstreamUnavailableException => HttpStatusCode.ServiceUnavailable,
                _ => HttpStatusCode.InternalServerError,
            };

            if (statusCode == HttpStatusCode.InternalServerError)
            {
                logger.LogError(exception, "Unhandled exception while processing {Method} {Path}", context.Request.Method, context.Request.Path);
            }
            else if (statusCode == HttpStatusCode.ServiceUnavailable)
            {
                // Logged, because a service that is down is worth knowing about — but as a
                // warning, since nothing here is a defect to go and fix. Logging it at error
                // level would train us to ignore errors.
                logger.LogWarning(exception, "Upstream service unavailable while processing {Method} {Path}", context.Request.Method, context.Request.Path);
            }

            context.Response.ContentType = "application/problem+json";
            context.Response.StatusCode = (int)statusCode;

            await context.Response.WriteAsJsonAsync(new
            {
                status = (int)statusCode,
                title = statusCode == HttpStatusCode.InternalServerError ? "An unexpected error occurred." : exception.Message,
            });
        }
    }
}
