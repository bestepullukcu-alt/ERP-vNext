using System.Text.RegularExpressions;

namespace Diten.Platform.Application.Features.ModulePages;

public static partial class ModulePageDescriptorNormalizer
{
    public static string NormalizePageCode(string? value) =>
        PageCodeUnderscoreRegex()
            .Replace(PageCodeCleanupRegex().Replace((value ?? string.Empty).Trim().ToUpperInvariant(), "_"), "_")
            .Trim('_');

    public static string NormalizeModuleCode(string? value) =>
        ModuleCodeCleanupRegex()
            .Replace((value ?? string.Empty).Trim().ToUpperInvariant(), "-")
            .Trim('-');

    public static string NormalizeRoutePath(string? value)
    {
        var route = (value ?? string.Empty).Trim();
        if (route.Length <= 1)
        {
            return route;
        }

        while (route.Contains("//", StringComparison.Ordinal))
        {
            route = route.Replace("//", "/", StringComparison.Ordinal);
        }

        route = "/" + route.Trim('/');
        return route.Length > 1 ? route.TrimEnd('/') : route;
    }

    public static string NormalizePermission(string? value)
    {
        var permission = PermissionWhitespaceRegex()
            .Replace((value ?? string.Empty).Trim().ToLowerInvariant(), string.Empty);

        while (permission.Contains("..", StringComparison.Ordinal))
        {
            permission = permission.Replace("..", ".", StringComparison.Ordinal);
        }

        return permission.Trim('.');
    }

    public static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string? NormalizeOptionalPermission(string? value)
    {
        var normalized = NormalizePermission(value);
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    [GeneratedRegex(@"[\s-]+|[^A-Z0-9_]+")]
    private static partial Regex PageCodeCleanupRegex();

    [GeneratedRegex(@"_+")]
    private static partial Regex PageCodeUnderscoreRegex();

    [GeneratedRegex(@"[\s_]+|[^A-Z0-9-]+")]
    private static partial Regex ModuleCodeCleanupRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex PermissionWhitespaceRegex();
}
