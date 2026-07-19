namespace PermissionGraph.Domain.Common;

public sealed class DomainRuleViolationException(string errorCode, string message) : Exception(message)
{
    public string ErrorCode { get; } = errorCode;
}
