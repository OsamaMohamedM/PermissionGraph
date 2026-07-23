namespace PermissionGraph.Api.Validation;

public sealed class CreateCustomRoleRequestValidator : AbstractValidator<CreateCustomRoleRequest>
{
    public CreateCustomRoleRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .MinimumLength(Role.NameMinLength)
            .MaximumLength(Role.NameMaxLength);

        RuleFor(request => request.Description)
            .MaximumLength(Role.DescriptionMaxLength);

        RuleFor(request => request.ScopeType)
            .NotEmpty()
            .Must(BeRoleScopeType)
            .WithMessage("Role scope type is invalid.");

        RuleFor(request => request.PermissionIds)
            .NotNull();

        RuleForEach(request => request.PermissionIds)
            .NotEmpty();
    }

    private static bool BeRoleScopeType(string? value)
    {
        return Enum.TryParse<RoleScopeType>(value, ignoreCase: true, out var parsed)
            && Enum.IsDefined(parsed);
    }
}

public sealed class UpdateCustomRoleRequestValidator : AbstractValidator<UpdateCustomRoleRequest>
{
    public UpdateCustomRoleRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .MinimumLength(Role.NameMinLength)
            .MaximumLength(Role.NameMaxLength);

        RuleFor(request => request.Description)
            .MaximumLength(Role.DescriptionMaxLength);
    }
}

public sealed class CloneRoleRequestValidator : AbstractValidator<CloneRoleRequest>
{
    public CloneRoleRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .MinimumLength(Role.NameMinLength)
            .MaximumLength(Role.NameMaxLength);

        RuleFor(request => request.Description)
            .MaximumLength(Role.DescriptionMaxLength);
    }
}

public sealed class ReplaceRolePermissionsRequestValidator : AbstractValidator<ReplaceRolePermissionsRequest>
{
    public ReplaceRolePermissionsRequestValidator()
    {
        RuleFor(request => request.PermissionIds)
            .NotNull();

        RuleForEach(request => request.PermissionIds)
            .NotEmpty();
    }
}

public sealed class ListRolesRequestValidator : AbstractValidator<ListRolesRequest>
{
    public ListRolesRequestValidator()
    {
        RuleFor(request => request.RoleType)
            .Must(value => value is null || BeEnumValue<RoleType>(value))
            .WithMessage("Role type is invalid.");

        RuleFor(request => request.ScopeType)
            .Must(value => value is null || BeEnumValue<RoleScopeType>(value))
            .WithMessage("Role scope type is invalid.");

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
