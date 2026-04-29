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
        normalized = SeparatorRegex().Replace(normalized, "-");
        normalized = normalized.Trim('-');
        return normalized;
    }

    [GeneratedRegex(@"[\s_\\-]+", RegexOptions.Compiled)]
    private static partial Regex SeparatorRegex();
}
