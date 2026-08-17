using System.Text.RegularExpressions;

namespace Diten.Platform.Application.Features.ModuleCatalog;

public static partial class ModuleCatalogCodeNormalizer
{
    public static string Normalize(string? moduleCode)
    {
        if (string.IsNullOrWhiteSpace(moduleCode))
        {
            return string.Empty;
        }

        var normalized = moduleCode.Trim().ToUpperInvariant();
        normalized = InvalidCharacterRegex().Replace(normalized, "-");
        normalized = SeparatorRegex().Replace(normalized, "-");
        normalized = normalized.Trim('-');
        return normalized;
    }

    [GeneratedRegex(@"[^A-Z0-9]+", RegexOptions.Compiled)]
    private static partial Regex InvalidCharacterRegex();

    [GeneratedRegex(@"-+", RegexOptions.Compiled)]
    private static partial Regex SeparatorRegex();
}
