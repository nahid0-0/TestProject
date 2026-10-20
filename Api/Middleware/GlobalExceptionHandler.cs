using Chess.Application.Common;
using Chess.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Chess.Api.Middleware;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title, detail) = exception switch
        {
            NotFoundException nfe => (StatusCodes.Status404NotFound, "Resource Not Found", nfe.Message),
            DomainException de => (StatusCodes.Status422UnprocessableEntity, "Domain Rule Violation", de.Message),
            ValidationException ve => (StatusCodes.Status400BadRequest, "Validation Error", ve.Message),
            _ => (StatusCodes.Status500InternalServerError, "Internal Server Error", "An unexpected error occurred.")
        };

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        };

        if (exception is ValidationException valEx && valEx.Errors.Count > 0)
        {
            problemDetails.Extensions["errors"] = valEx.Errors;
        }

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }
}
