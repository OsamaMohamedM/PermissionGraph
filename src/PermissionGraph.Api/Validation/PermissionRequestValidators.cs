using FluentValidation;
using PermissionGraph.Api.Endpoints;
using PermissionGraph.Contracts.Permissions;
using PermissionGraph.Domain.Permissions;

namespace PermissionGraph.Api.Validation;

public sealed class CreateCustomPermissionRequestValidator : AbstractValidator<CreateCustomPermissionRequest>
{
    public CreateCustomPermissionRequestValidator()
    {
        RuleFor(request => request.Key)
            .NotEmpty()
            .MinimumLength(PermissionDefinition.KeyMinLength)
            .MaximumLength(PermissionDefinition.KeyMaxLength)
            .Matches("^[a-z][a-z0-9]*(\\.[a-z][a-z0-9_]*)+$")
            .WithMessage("Permission key format is invalid.")
            .Must(key => key is null || !key.StartsWith(PermissionDefinition.ReservedPlatformPrefix, StringComparison.Ordinal))
            .WithMessage("Custom permission key cannot use the reserved platform prefix.");

        RuleFor(request => request.DisplayName)
            .NotEmpty()
            .MinimumLength(PermissionDefinition.DisplayNameMinLength)
            .MaximumLength(PermissionDefinition.DisplayNameMaxLength);

        RuleFor(request => request.Description)
            .MaximumLength(PermissionDefinition.DescriptionMaxLength);

        RuleFor(request => request.Module)
            .NotEmpty()
            .MinimumLength(PermissionDefinition.ModuleMinLength)
            .MaximumLength(PermissionDefinition.ModuleMaxLength);

        RuleFor(request => request.AllowedScopes)
            .NotEmpty()
            .Must(BeAllowedScope)
            .WithMessage("Allowed scope is invalid.");
    }

    private static bool BeAllowedScope(string? value)
    {
        return Enum.TryParse<PermissionAllowedScopes>(value, ignoreCase: true, out var parsed)
            && Enum.IsDefined(parsed);
    }
}

public sealed class UpdateCustomPermissionRequestValidator : AbstractValidator<UpdateCustomPermissionRequest>
{
    public UpdateCustomPermissionRequestValidator()
    {
        RuleFor(request => request.DisplayName)
            .NotEmpty()
            .MinimumLength(PermissionDefinition.DisplayNameMinLength)
            .MaximumLength(PermissionDefinition.DisplayNameMaxLength);

        RuleFor(request => request.Description)
            .MaximumLength(PermissionDefinition.DescriptionMaxLength);

        RuleFor(request => request.Module)
            .NotEmpty()
            .MinimumLength(PermissionDefinition.ModuleMinLength)
            .MaximumLength(PermissionDefinition.ModuleMaxLength);
    }
}

public sealed class ListPermissionsRequestValidator : AbstractValidator<ListPermissionsRequest>
{
    public ListPermissionsRequestValidator()
    {
        RuleFor(request => request.PermissionType)
            .Must(value => value is null || BeEnumValue<PermissionType>(value))
            .WithMessage("Permission type is invalid.");

        RuleFor(request => request.Module)
            .MaximumLength(PermissionDefinition.ModuleMaxLength);

        RuleFor(request => request.AllowedScope)
            .Must(value => value is null || BeEnumValue<PermissionAllowedScopes>(value))
            .WithMessage("Allowed scope is invalid.");

        RuleFor(request => request.Search)
            .MaximumLength(200);

        RuleFor(request => request.Page)
            .GreaterThanOrEqualTo(1);

        RuleFor(request => request.PageSize)
            .InclusiveBetween(1, 100);
    }

    private static bool BeEnumValue<TEnum>(string value)
        where TEnum : struct, Enum
    {
        return Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed)
            && Enum.IsDefined(parsed);
    }
}
