using System.Text.RegularExpressions;

namespace Diten.Platform.Application.Features.InterfaceRegistry;

public static partial class InterfaceCodeNormalizer
{
    public static string Normalize(string? interfaceCode)
    {
        if (string.IsNullOrWhiteSpace(interfaceCode))
        {
            return string.Empty;
        }

        var normalized = WhitespaceRegex().Replace(interfaceCode.Trim().ToUpperInvariant(), string.Empty);
        return DotRegex().Replace(normalized, ".");
    }

    public static bool IsValid(string? interfaceCode)
    {
        var normalized = Normalize(interfaceCode);
        return InterfaceCodeRegex().IsMatch(normalized);
    }

    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"\.+", RegexOptions.Compiled)]
    private static partial Regex DotRegex();

    [GeneratedRegex(@"^[A-Z0-9_]+(\.[A-Z0-9_]+){2,}$", RegexOptions.Compiled)]
    private static partial Regex InterfaceCodeRegex();
}
