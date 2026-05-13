using System.Text.RegularExpressions;

namespace Diten.Platform.Application.Features.InterfaceRegistry;

public static partial class EndpointKeyNormalizer
{
    public static string NormalizeRoute(string? routePath)
    {
        if (string.IsNullOrWhiteSpace(routePath))
        {
            return string.Empty;
        }

        var route = routePath.Trim().ToLowerInvariant().Replace('\\', '/');
        route = SlashRegex().Replace(route, "/");
        if (!route.StartsWith("/", StringComparison.Ordinal))
        {
            route = "/" + route;
        }

        return route.Length > 1 ? route.TrimEnd('/') : route;
    }

    public static string Create(string? httpMethod, string? routePath, string? version)
    {
        var method = string.IsNullOrWhiteSpace(httpMethod) ? string.Empty : httpMethod.Trim().ToUpperInvariant();
        var route = NormalizeRoute(routePath);
        var normalizedVersion = string.IsNullOrWhiteSpace(version) ? string.Empty : version.Trim().ToLowerInvariant();
        return $"{method}:{route}:{normalizedVersion}";
    }

    public static bool IsValid(string? httpMethod, string? routePath, string? version)
    {
        var method = string.IsNullOrWhiteSpace(httpMethod) ? string.Empty : httpMethod.Trim().ToUpperInvariant();
        var route = NormalizeRoute(routePath);
        var normalizedVersion = string.IsNullOrWhiteSpace(version) ? string.Empty : version.Trim().ToLowerInvariant();

        return HttpMethodRegex().IsMatch(method)
               && RouteRegex().IsMatch(route)
               && VersionRegex().IsMatch(normalizedVersion);
    }

    [GeneratedRegex(@"/+", RegexOptions.Compiled)]
    private static partial Regex SlashRegex();

    [GeneratedRegex(@"^(GET|POST|PUT|PATCH|DELETE|OPTIONS|HEAD)$", RegexOptions.Compiled)]
    private static partial Regex HttpMethodRegex();

    [GeneratedRegex(@"^/[a-z0-9/_{}.:~-]*[a-z0-9_{}]$|^/$", RegexOptions.Compiled)]
    private static partial Regex RouteRegex();

    [GeneratedRegex(@"^v[0-9]+$", RegexOptions.Compiled)]
    private static partial Regex VersionRegex();
}
