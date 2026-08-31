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
}
