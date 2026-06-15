using System.Text;

namespace Diten.Platform.Application.Features.TenantOrganization;

public static class OrganizationCodeNormalizer
{
    public static string Normalize(string value)
    {
        var source = value.Trim().ToUpperInvariant();
        var builder = new StringBuilder(source.Length);
        var previousWasSeparator = false;

        foreach (var c in source)
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(c);
                previousWasSeparator = false;
                continue;
            }

            if (!previousWasSeparator)
            {
                builder.Append('-');
                previousWasSeparator = true;
            }
        }

        return builder.ToString().Trim('-');
    }
}
