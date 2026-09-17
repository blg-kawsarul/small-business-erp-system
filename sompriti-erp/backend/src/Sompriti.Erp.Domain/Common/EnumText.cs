using System.Collections.Concurrent;
using System.Text;

namespace Sompriti.Erp.Domain.Common;

/// <summary>Converts enum values between C# PascalCase and UPPER_SNAKE_CASE text.</summary>
public static class EnumText
{
    private static readonly ConcurrentDictionary<(Type, string), object> Parsed = new();

    public static string ToText<TEnum>(TEnum value) where TEnum : struct, Enum => ToText(value.ToString());

    public static string ToText(string pascal)
    {
        var sb = new StringBuilder(pascal.Length + 4);
        for (var i = 0; i < pascal.Length; i++)
        {
            var c = pascal[i];
            if (i > 0 && char.IsUpper(c)) sb.Append('_');
            sb.Append(char.ToUpperInvariant(c));
        }
        return sb.ToString();
    }

    public static TEnum Parse<TEnum>(string text) where TEnum : struct, Enum
    {
        if (TryParse<TEnum>(text, out var value)) return value;
        throw new ArgumentException($"'{text}' is not a valid {typeof(TEnum).Name}.");
    }

    public static bool TryParse<TEnum>(string? text, out TEnum value) where TEnum : struct, Enum
    {
        value = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        if (Parse(typeof(TEnum), text) is TEnum e) { value = e; return true; }
        return false;
    }

    public static object? Parse(Type enumType, string text)
    {
        var key = (enumType, text.Trim().ToUpperInvariant());
        if (Parsed.TryGetValue(key, out var cached)) return cached;
        foreach (var name in Enum.GetNames(enumType))
        {
            if (string.Equals(ToText(name), key.Item2, StringComparison.Ordinal)
                || string.Equals(name, text.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                var v = Enum.Parse(enumType, name);
                Parsed[key] = v;
                return v;
            }
        }
        return null;
    }
}
