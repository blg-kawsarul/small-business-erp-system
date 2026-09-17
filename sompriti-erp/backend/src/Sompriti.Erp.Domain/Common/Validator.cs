namespace Sompriti.Erp.Domain.Common;

/// <summary>Small fluent helper that collects field errors and throws one validation exception.</summary>
public sealed class Validator
{
    private readonly Dictionary<string, List<string>> _errors = new();

    public bool HasErrors => _errors.Count > 0;

    public Validator Add(string field, string message)
    {
        if (!_errors.TryGetValue(field, out var list)) _errors[field] = list = new List<string>();
        list.Add(message);
        return this;
    }

    public Validator Required(string field, string? value, string label, int maxLength = 0)
    {
        if (string.IsNullOrWhiteSpace(value)) return Add(field, $"{label} is required.");
        if (maxLength > 0 && value.Trim().Length > maxLength) Add(field, $"{label} must be at most {maxLength} characters.");
        return this;
    }

    public Validator MaxLength(string field, string? value, string label, int maxLength)
    {
        if (value is not null && value.Trim().Length > maxLength) Add(field, $"{label} must be at most {maxLength} characters.");
        return this;
    }

    public Validator Required(string field, Guid? value, string label)
    {
        if (value is null || value == Guid.Empty) Add(field, $"{label} is required.");
        return this;
    }

    public Validator When(bool condition, string field, string message)
    {
        if (condition) Add(field, message);
        return this;
    }

    public void ThrowIfInvalid()
    {
        if (!HasErrors) return;
        var dict = _errors.ToDictionary(k => k.Key, v => v.Value.ToArray());
        var first = _errors.First().Value.First();
        throw new DomainException(ErrorKind.Validation, ErrorCodes.Validation, first, dict);
    }

    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
