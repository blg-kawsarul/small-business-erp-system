namespace Sompriti.Erp.Domain.Common;

/// <summary>Standard business error codes returned to the client.</summary>
public static class ErrorCodes
{
    public const string Validation = "VALIDATION_ERROR";
    public const string NotFound = "NOT_FOUND";
    public const string Forbidden = "FORBIDDEN";
    public const string RevisionConflict = "REVISION_CONFLICT";
    public const string InsufficientStock = "INSUFFICIENT_STOCK";
    public const string InvalidStatusTransition = "INVALID_STATUS_TRANSITION";
    public const string OrderHasPayments = "ORDER_HAS_PAYMENTS";
    public const string Overpayment = "OVERPAYMENT";
    public const string DuplicateCode = "DUPLICATE_CODE";
    public const string InUseByDraft = "IN_USE_BY_DRAFT";
    public const string NegativeStockOnVoid = "NEGATIVE_STOCK_ON_VOID";
    public const string NegativeStock = "NEGATIVE_STOCK";
    public const string InvalidCredentials = "INVALID_CREDENTIALS";
    public const string AccountLocked = "ACCOUNT_LOCKED";
    public const string InvalidToken = "INVALID_TOKEN";
    public const string BusinessRule = "BUSINESS_RULE";
}

public enum ErrorKind
{
    Validation,      // 400
    Unauthorized,    // 401
    Forbidden,       // 403
    NotFound,        // 404
    Conflict,        // 409
    BusinessRule     // 422
}

/// <summary>Exception carrying a business error code, a kind (mapped to HTTP status) and optional field errors.</summary>
public class DomainException : Exception
{
    public string Code { get; }
    public ErrorKind Kind { get; }
    public IReadOnlyDictionary<string, string[]> Errors { get; }
    public object? Details { get; }

    public DomainException(ErrorKind kind, string code, string message,
        IReadOnlyDictionary<string, string[]>? errors = null, object? details = null) : base(message)
    {
        Kind = kind;
        Code = code;
        Errors = errors ?? new Dictionary<string, string[]>();
        Details = details;
    }

    public static DomainException NotFound(string what) =>
        new(ErrorKind.NotFound, ErrorCodes.NotFound, $"{what} was not found.");

    public static DomainException Forbidden(string message = "You do not have permission to perform this action.") =>
        new(ErrorKind.Forbidden, ErrorCodes.Forbidden, message);

    public static DomainException RevisionConflict() =>
        new(ErrorKind.Conflict, ErrorCodes.RevisionConflict,
            "This record was changed by someone else. Please reload and try again.");

    public static DomainException Rule(string code, string message, object? details = null) =>
        new(ErrorKind.BusinessRule, code, message, null, details);

    public static DomainException Conflict(string code, string message) =>
        new(ErrorKind.Conflict, code, message);

    public static DomainException Validation(string field, string message) =>
        new(ErrorKind.Validation, ErrorCodes.Validation, message,
            new Dictionary<string, string[]> { [field] = new[] { message } });
}
