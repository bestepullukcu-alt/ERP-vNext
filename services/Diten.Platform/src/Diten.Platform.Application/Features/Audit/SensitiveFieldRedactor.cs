using System.Collections;
using System.Reflection;
using System.Text.Json;
using Diten.Platform.Application.Contracts.Audit;

namespace Diten.Platform.Application.Features.Audit;

public sealed class SensitiveFieldRedactor : ISensitiveFieldRedactor
{
    private const int MaxDepth = 32;
    private readonly IReadOnlyList<SensitiveFieldRule> _rules;

    public SensitiveFieldRedactor(ISensitiveFieldRedactionRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _rules = registry.GetRules();
    }

    public object? Redact(object? value)
    {
        return RedactValue(value, [], depth: 0);
    }

    public IReadOnlyDictionary<string, object?> RedactDictionary(IReadOnlyDictionary<string, object?>? value)
    {
        return value is null
            ? new Dictionary<string, object?>()
            : RedactDictionaryValue(value, [], depth: 0);
    }

    private object? RedactValue(object? value, IReadOnlyList<string> path, int depth)
    {
        if (value is null || IsScalar(value) || depth >= MaxDepth)
        {
            return value;
        }

        if (value is JsonElement jsonElement)
        {
            return RedactJsonElement(jsonElement, path, depth);
        }

        if (value is IReadOnlyDictionary<string, object?> readOnlyDictionary)
        {
            return RedactDictionaryValue(readOnlyDictionary, path, depth + 1);
        }

        if (value is IDictionary<string, object?> dictionary)
        {
            return RedactDictionaryValue(dictionary, path, depth + 1);
        }

        if (value is IDictionary nonGenericDictionary)
        {
            return RedactDictionaryValue(nonGenericDictionary, path, depth + 1);
        }

        if (value is IEnumerable enumerable && value is not string)
        {
            var items = new List<object?>();
            foreach (var item in enumerable)
            {
                items.Add(RedactValue(item, path, depth + 1));
            }

            return items;
        }

        return RedactObject(value, path, depth + 1);
    }

    private Dictionary<string, object?> RedactDictionaryValue(
        IEnumerable<KeyValuePair<string, object?>> dictionary,
        IReadOnlyList<string> path,
        int depth)
    {
        var redacted = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var pair in dictionary)
        {
            var key = pair.Key;
            var childPath = AppendPath(path, key);
            redacted[key] = FindRule(key, childPath) is { } rule
                ? rule.Replacement
                : RedactValue(pair.Value, childPath, depth + 1);
        }

        return redacted;
    }

    private Dictionary<string, object?> RedactDictionaryValue(
        IDictionary nonGenericDictionary,
        IReadOnlyList<string> path,
        int depth)
    {
        var redacted = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (DictionaryEntry entry in nonGenericDictionary)
        {
            if (entry.Key is not string key)
            {
                continue;
            }

            var childPath = AppendPath(path, key);
            redacted[key] = FindRule(key, childPath) is { } rule
                ? rule.Replacement
                : RedactValue(entry.Value, childPath, depth + 1);
        }

        return redacted;
    }

    private object? RedactJsonElement(JsonElement element, IReadOnlyList<string> path, int depth)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => RedactJsonObject(element, path, depth + 1),
            JsonValueKind.Array => element.EnumerateArray()
                .Select(item => RedactJsonElement(item, path, depth + 1))
                .ToList(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => TryReadNumber(element),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.GetRawText()
        };
    }

    private Dictionary<string, object?> RedactJsonObject(JsonElement element, IReadOnlyList<string> path, int depth)
    {
        var redacted = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var property in element.EnumerateObject())
        {
            var childPath = AppendPath(path, property.Name);
            redacted[property.Name] = FindRule(property.Name, childPath) is { } rule
                ? rule.Replacement
                : RedactJsonElement(property.Value, childPath, depth + 1);
        }

        return redacted;
    }

    private Dictionary<string, object?> RedactObject(object value, IReadOnlyList<string> path, int depth)
    {
        var redacted = new Dictionary<string, object?>(StringComparer.Ordinal);
        var properties = value.GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.GetIndexParameters().Length == 0 && property.CanRead);

        foreach (var property in properties)
        {
            var childPath = AppendPath(path, property.Name);
            redacted[property.Name] = FindRule(property.Name, childPath) is { } rule
                ? rule.Replacement
                : RedactValue(property.GetValue(value), childPath, depth + 1);
        }

        return redacted;
    }

    private SensitiveFieldRule? FindRule(string key, IReadOnlyList<string> path)
    {
        var normalizedKey = Normalize(key);
        var normalizedPath = Normalize(string.Join(".", path));

        return _rules.FirstOrDefault(rule =>
        {
            var normalizedPattern = Normalize(rule.Pattern);
            if (rule.Pattern.Contains('.', StringComparison.Ordinal))
            {
                return string.Equals(normalizedPath, normalizedPattern, StringComparison.Ordinal);
            }

            return string.Equals(normalizedKey, normalizedPattern, StringComparison.Ordinal)
                   || normalizedKey.Contains(normalizedPattern, StringComparison.Ordinal);
        });
    }

    private static IReadOnlyList<string> AppendPath(IReadOnlyList<string> path, string segment)
    {
        var next = new string[path.Count + 1];
        for (var i = 0; i < path.Count; i++)
        {
            next[i] = path[i];
        }

        next[^1] = segment;
        return next;
    }

    private static object TryReadNumber(JsonElement element)
    {
        if (element.TryGetInt64(out var longValue))
        {
            return longValue;
        }

        if (element.TryGetDecimal(out var decimalValue))
        {
            return decimalValue;
        }

        return element.GetRawText();
    }

    private static bool IsScalar(object value)
    {
        var type = value.GetType();
        return type.IsPrimitive
               || type.IsEnum
               || value is string
               || value is decimal
               || value is Guid
               || value is DateTime
               || value is DateTimeOffset
               || value is TimeSpan;
    }

    private static string Normalize(string value)
    {
        return new string(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }
}
