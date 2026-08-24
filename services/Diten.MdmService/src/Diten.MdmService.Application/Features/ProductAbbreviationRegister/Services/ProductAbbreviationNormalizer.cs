using System.Text.RegularExpressions;

namespace Diten.MdmService.Application.Features.ProductAbbreviationRegister.Services;

public static partial class ProductAbbreviationNormalizer
{
    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = (value ?? string.Empty).Trim().ToUpperInvariant();
        return Grammar().IsMatch(normalized);
    }

    [GeneratedRegex("^[A-Z]{3}$", RegexOptions.CultureInvariant)]
    private static partial Regex Grammar();
}
