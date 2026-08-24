using System.Text;

namespace Diten.MdmService.Domain.Entities;

public static class GlobalProductNameRules
{
    public const int MaximumLength = 200;

    public static string CleanVisible(string value) => value.Trim();

    public static string NormalizeDuplicateKey(string value)
        => CleanVisible(value).Normalize(NormalizationForm.FormKC).ToUpperInvariant();

    public static bool HasValidLength(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var length = CleanVisible(value).EnumerateRunes().Count();
        return length is >= 1 and <= MaximumLength;
    }
}
