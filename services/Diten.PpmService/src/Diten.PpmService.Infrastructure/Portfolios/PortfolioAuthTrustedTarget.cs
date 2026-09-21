using System.Globalization;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Diten.PpmService.Infrastructure.Portfolios;

// A single operator-approved origin plus normal TLS server authentication; HTTPS syntax alone is not approval.
public sealed class PortfolioAuthTrustedTarget(IOptions<PortfolioAuthorityOptions> options, IHostEnvironment? environment)
{
    public bool IsEnabled => options.Value.IsValid && environment is not null &&
        !string.Equals(environment.EnvironmentName, Environments.Production, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(environment.EnvironmentName, options.Value.NonProductionEnvironmentName, StringComparison.Ordinal);

    public static bool IsConfigurationValid(PortfolioAuthorityOptions value) => !value.Enabled ||
        (value.TimeoutSeconds is > 0 and <= 30 &&
         !string.IsNullOrWhiteSpace(value.NonProductionEnvironmentName) &&
         value.NonProductionEnvironmentName == value.NonProductionEnvironmentName.Trim() &&
         !string.Equals(value.NonProductionEnvironmentName, Environments.Production, StringComparison.OrdinalIgnoreCase) &&
         !string.IsNullOrWhiteSpace(value.TrustProfileOwner) &&
         !string.IsNullOrWhiteSpace(value.TrustProfileApprovalReference) &&
         TryOrigin(value.ApprovedAuthOrigin, out _));

    public bool TryCreateRequestUri(string path, out Uri uri)
    {
        uri = null!;
        if (!IsEnabled || !TryOrigin(options.Value.ApprovedAuthOrigin, out var origin) || !IsAllowedPath(path)) return false;
        uri = new Uri(origin!, path);
        return uri.Scheme == Uri.UriSchemeHttps && uri.IdnHost == origin!.IdnHost && uri.Port == origin.Port &&
            string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Fragment);
    }

    public static HttpClientHandler CreateHttpHandler() => new()
    {
        AllowAutoRedirect = false,
        UseProxy = false,
        UseCookies = false,
        CheckCertificateRevocationList = true
        // No certificate callback: the platform validates hostname, chain and validity normally.
    };

    private static bool TryOrigin(string? value, out Uri? origin)
    {
        origin = null;
        return !string.IsNullOrWhiteSpace(value) && value == value.Trim() &&
            !value.Contains('\\') && !value.Contains('%') &&
            Uri.TryCreate(value, UriKind.Absolute, out origin) && origin.Scheme == Uri.UriSchemeHttps &&
            origin.HostNameType is UriHostNameType.Dns or UriHostNameType.IPv4 or UriHostNameType.IPv6 &&
            origin.Port > 0 && string.IsNullOrEmpty(origin.UserInfo) && origin.AbsolutePath == "/" &&
            string.IsNullOrEmpty(origin.Query) && string.IsNullOrEmpty(origin.Fragment);
    }

    private static bool IsAllowedPath(string path)
    {
        if (path.StartsWith("api/users/lookup?", StringComparison.Ordinal))
        {
            var query = QueryHelpers.ParseQuery(path["api/users/lookup".Length..]);
            return !path.Contains('#') && query.Count == 2 && query.TryGetValue("search", out var search) && search.Count == 1 &&
                query.TryGetValue("limit", out var limit) && limit.Count == 1 &&
                int.TryParse(limit[0], NumberStyles.None, CultureInfo.InvariantCulture, out var count) && count is >= 1 and <= 20;
        }
        var parts = path.Split('/');
        return parts.Length == 4 && parts[0] == "api" && parts[1] == "users" &&
            Guid.TryParseExact(parts[2], "D", out var id) && id != Guid.Empty &&
            parts[3] is "account-assertion" or "display-label";
    }
}
