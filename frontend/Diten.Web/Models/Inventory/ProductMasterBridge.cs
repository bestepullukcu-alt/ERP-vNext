using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Diten.Web.Models.Inventory;

/// <summary>
/// WP-0173-PM0 — DEVELOPMENT-ONLY bridge for the Web's PRODUCT-MASTER reads (FU-PM-01; owner decision 2026-10-02;
/// docs/roadmap/plans/mod-0290-product-master-bundle-requirements-for-mvp1-2026-10-02.md §6).
/// <para>
/// The Gateway has no <c>/api/product-master</c> route until MDM (MOD-0290) serves PRODUCT-MASTER-BUNDLE v1, so the S-3
/// form cannot read units / lot / serial flags. With <c>Inventory:ProductMasterBaseUrl</c> set (environment
/// <c>Inventory__ProductMasterBaseUrl</c>, never an appsettings value) AND the host in Development, the Web's
/// <c>/api/product-master/…</c> reads go to that address with the prefix removed — the Prism mock of the frozen contract
/// serves it from the root, as InventoryService's <c>Inventory__Upstream__ProductMasterBaseUrl</c> already does.
/// </para>
/// <para>
/// Empty (the default, every environment) = today's behaviour, the Gateway. Set outside Development = IGNORED with a
/// warning: a mock must never answer a non-development Web (configuration-safety). Removal: delete this class and the
/// two lines that use it once MDM serves the contract through the Gateway.
/// </para>
/// </summary>
public static class ProductMasterBridge
{
    public const string SettingKey = "Inventory:ProductMasterBaseUrl";
    public const string GatewayPrefix = "/api/product-master";

    private static int _warned;

    /// <summary>The bridge base URL to use for this request, or null for the Gateway.</summary>
    public static string? Resolve(IServiceProvider? services)
    {
        if (services is null) return null;
        var configuration = services.GetService<IConfiguration>();
        var environment = services.GetService<IWebHostEnvironment>();
        // Per request this is asked again; the warning for an ignored setting is logged once per process.
        var logger = Volatile.Read(ref _warned) == 0
            ? services.GetService<ILoggerFactory>()?.CreateLogger(typeof(ProductMasterBridge).FullName!)
            : null;
        var url = Resolve(configuration?[SettingKey], environment?.EnvironmentName, logger, out var ignored);
        if (ignored) Interlocked.Exchange(ref _warned, 1);
        return url;
    }

    public static string? Resolve(string? configured, string? environmentName, ILogger? logger) =>
        Resolve(configured, environmentName, logger, out _);

    private static string? Resolve(string? configured, string? environmentName, ILogger? logger, out bool ignored)
    {
        ignored = false;
        if (string.IsNullOrWhiteSpace(configured)) return null;

        if (!string.Equals(environmentName, Environments.Development, StringComparison.Ordinal))
        {
            // The setting is present, unused, and someone should know.
            ignored = true;
            logger?.LogWarning("{Setting} is set but the environment is {Environment}: the development product-master bridge is IGNORED and the Gateway is used.",
                SettingKey, environmentName ?? "(none)");
            return null;
        }

        if (!Uri.TryCreate(configured.Trim(), UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            ignored = true;
            logger?.LogWarning("{Setting} is not an absolute http(s) URL: the development product-master bridge is IGNORED.", SettingKey);
            return null;
        }

        return uri.ToString().TrimEnd('/');
    }

    /// <summary>
    /// The absolute URL for a call: a <c>/api/product-master/…</c> path goes to the bridge without the prefix when a
    /// bridge is active; everything else (and every call without a bridge) goes to the Gateway unchanged.
    /// </summary>
    public static string Target(string gatewayUrl, string? bridgeUrl, string pathAndQuery)
    {
        if (bridgeUrl is not null && IsProductMaster(pathAndQuery))
            return bridgeUrl + pathAndQuery[GatewayPrefix.Length..];
        return gatewayUrl + pathAndQuery;
    }

    public static bool IsProductMaster(string pathAndQuery) =>
        pathAndQuery.StartsWith(GatewayPrefix + "/", StringComparison.Ordinal)
        || pathAndQuery.StartsWith(GatewayPrefix + "?", StringComparison.Ordinal)
        || string.Equals(pathAndQuery, GatewayPrefix, StringComparison.Ordinal);
}
