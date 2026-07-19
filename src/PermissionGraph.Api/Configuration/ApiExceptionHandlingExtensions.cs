using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PermissionGraph.Application.Common.Errors;

namespace PermissionGraph.Api.Configuration;

public static class ApiExceptionHandlingExtensions
{
    public static IServiceCollection AddApiExceptionHandling(this IServiceCollection services)
    {
        services.AddProblemDetails();
        services.AddExceptionHandler<ApiExceptionHandler>();

        return services;
    }
}

internal sealed class ApiExceptionHandler(
    IHostEnvironment environment,
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var problem = CreateProblem(httpContext, exception);

        if (problem.Status == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled API exception");
        }

        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        httpContext.Response.ContentType = "application/problem+json";

        if (problem is HttpValidationProblemDetails validationProblem)
        {
            await httpContext.Response.WriteAsJsonAsync(
                validationProblem,
                options: null,
                contentType: "application/problem+json",
                cancellationToken);
        }
        else
        {
            await httpContext.Response.WriteAsJsonAsync(
                problem,
                options: null,
                contentType: "application/problem+json",
                cancellationToken);
        }

        return true;
    }

    private ProblemDetails CreateProblem(HttpContext context, Exception exception)
    {
        ProblemDetails problem = exception switch
        {
            RequestValidationException validation => ToValidationProblem(validation.Errors, validation.SafeMessage),
            CommandValidationException validation => ToValidationProblem(validation.Errors, validation.SafeMessage),
            ValidationException fluentValidation => ToValidationProblem(ToErrors(fluentValidation), "Request validation failed."),
            BadRequestApplicationException badRequest => ToProblem(StatusCodes.Status400BadRequest, badRequest),
            UnauthorizedApplicationException unauthorized => ToProblem(StatusCodes.Status401Unauthorized, unauthorized),
            ForbiddenApplicationException forbidden => ToProblem(StatusCodes.Status403Forbidden, forbidden),
            NotFoundApplicationException notFound => ToProblem(StatusCodes.Status404NotFound, notFound),
            ConflictApplicationException conflict => ToProblem(StatusCodes.Status409Conflict, conflict),
            _ => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred."
            }
        };

        problem.Instance = context.Request.Path;
        problem.Extensions["traceId"] = context.TraceIdentifier;

        if (environment.IsDevelopment() && exception is not ApplicationErrorException)
        {
            problem.Detail = exception.Message;
        }

        return problem;
    }

    private static ProblemDetails ToProblem(int status, ApplicationErrorException exception)
    {
        var problem = new ProblemDetails
        {
            Status = status,
            Title = exception.SafeMessage
        };

        problem.Extensions["code"] = exception.ErrorCode;
        return problem;
    }

    private static HttpValidationProblemDetails ToValidationProblem(
        IReadOnlyDictionary<string, string[]> errors,
        string title)
    {
        return new HttpValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = title
        };
    }

    private static Dictionary<string, string[]> ToErrors(ValidationException exception)
    {
        return exception.Errors
            .GroupBy(error => error.PropertyName)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.ErrorMessage).ToArray());
    }
}
