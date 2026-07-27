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
                _ => HttpStatusCode.InternalServerError,
            };

            if (statusCode == HttpStatusCode.InternalServerError)
            {
                logger.LogError(exception, "Unhandled exception while processing {Method} {Path}", context.Request.Method, context.Request.Path);
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
