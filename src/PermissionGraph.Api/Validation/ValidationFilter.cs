using FluentValidation;
using PermissionGraph.Application.Common.Errors;

namespace PermissionGraph.Api.Validation;

public sealed class ValidationFilter<TRequest> : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.Arguments.OfType<TRequest>().FirstOrDefault();
        if (request is null)
        {
            throw new InvalidOperationException($"No argument of type {typeof(TRequest).Name} was found.");
        }

        var validator = context.HttpContext.RequestServices.GetRequiredService<IValidator<TRequest>>();

        var result = await validator.ValidateAsync(request, context.HttpContext.RequestAborted);
        if (result.IsValid)
        {
            return await next(context);
        }

        var errors = result.Errors
            .GroupBy(error => error.PropertyName)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.ErrorMessage).ToArray());

        throw new RequestValidationException(errors);
    }
}
