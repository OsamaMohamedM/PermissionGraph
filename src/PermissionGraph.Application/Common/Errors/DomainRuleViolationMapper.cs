using PermissionGraph.Domain.Common;

namespace PermissionGraph.Application.Common.Errors;

internal static class DomainRuleViolationMapper
{
    public static ConflictApplicationException ToConflict(DomainRuleViolationException exception)
    {
        return new ConflictApplicationException(exception.ErrorCode, exception.Message);
    }
}
