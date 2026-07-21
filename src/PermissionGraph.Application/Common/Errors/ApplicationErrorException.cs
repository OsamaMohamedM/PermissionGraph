namespace PermissionGraph.Application.Common.Errors;

public abstract class ApplicationErrorException(string errorCode, string safeMessage) : Exception(safeMessage)
{
    public string ErrorCode { get; } = errorCode;

    public string SafeMessage { get; } = safeMessage;
}

public sealed class RequestValidationException(IReadOnlyDictionary<string, string[]> errors)
    : ApplicationErrorException("validation_failed", "Request validation failed.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}

public sealed class CommandValidationException(IReadOnlyDictionary<string, string[]> errors)
    : ApplicationErrorException("command_validation_failed", "Request validation failed.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}

public sealed class UnauthorizedApplicationException(string errorCode, string safeMessage)
    : ApplicationErrorException(errorCode, safeMessage);

public sealed class ForbiddenApplicationException(string errorCode, string safeMessage)
    : ApplicationErrorException(errorCode, safeMessage);

public sealed class NotFoundApplicationException(string errorCode, string safeMessage)
    : ApplicationErrorException(errorCode, safeMessage);

public sealed class ConflictApplicationException(string errorCode, string safeMessage)
    : ApplicationErrorException(errorCode, safeMessage);

public sealed class BadRequestApplicationException(string errorCode, string safeMessage)
    : ApplicationErrorException(errorCode, safeMessage);