using System.Text;

namespace Diten.Platform.Application.Features.HrisSources;

public static class HrisSourceCodeNormalizer
{
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var character in value.Trim().ToUpperInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                continue;
            }

            if (character is '-' or '_')
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }
}
