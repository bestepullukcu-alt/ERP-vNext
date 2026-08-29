namespace Diten.AuthService.Domain.S2S;

public static class S2SExactValue
{
    public static string RequiredLowercase(string value, string parameterName)
    {
        Required(value, parameterName);
        if (!string.Equals(value, value.ToLowerInvariant(), StringComparison.Ordinal))
            throw new S2SContractException("Value must use its exact lowercase representation.", parameterName);

        return value;
    }

    public static string Required(string value, string parameterName)
    {
        if (string.IsNullOrEmpty(value) || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
            throw new S2SContractException("Value must be non-empty and contain no surrounding whitespace.", parameterName);
        if (value.Contains('*', StringComparison.Ordinal))
            throw new S2SContractException("Wildcard values are forbidden.", parameterName);

        return value;
    }

    public static IReadOnlyList<string> RequiredDistinctLowercase(IEnumerable<string> values, string parameterName)
    {
        var materialized = values?.Select(x => RequiredLowercase(x, parameterName)).ToArray()
            ?? throw new S2SContractException("Value collection is required.", parameterName);
        if (materialized.Length == 0 || materialized.Distinct(StringComparer.Ordinal).Count() != materialized.Length)
            throw new S2SContractException("Value collection must be non-empty and contain exact distinct values.", parameterName);

        return Array.AsReadOnly(materialized);
    }

    public static IReadOnlyList<string> RequiredDistinct(IEnumerable<string> values, string parameterName)
    {
        var materialized = values?.Select(x => Required(x, parameterName)).ToArray()
            ?? throw new S2SContractException("Value collection is required.", parameterName);
        if (materialized.Length == 0 || materialized.Distinct(StringComparer.Ordinal).Count() != materialized.Length)
            throw new S2SContractException("Value collection must be non-empty and contain exact distinct values.", parameterName);

        return Array.AsReadOnly(materialized);
    }
}
