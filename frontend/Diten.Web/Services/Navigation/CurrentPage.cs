using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace Diten.Web.Services.Navigation;

/// <summary>
/// WP-UX-COMMON-01 (BL-495) — which page the shell is drawing, as the STABLE code the sidebar and Ctrl+K already use
/// (the Nav L10n bridge: <c>Nav.Page.{code}</c>), and whether the body drew the unauthorized face.
///
/// <para><b>Where the code comes from.</b> The tenant shell's menu component (<c>DynamicModuleMenu</c>) already holds
/// every entitled page with its <c>PageCode</c> and <c>RoutePath</c>, and it renders BEFORE the top bar. It records
/// the page whose route is the LONGEST match of the request path on a segment boundary (<c>/Tasks/Create</c> beats
/// <c>/Tasks</c>; <c>/TasksArchive</c> matches neither). A view may override with <c>ViewData["PageCode"]</c>. No
/// menu (it failed to load, or nothing matched) means no code: the help panel then shows its "being prepared" text.</para>
///
/// <para><b>Platform admin pages</b> have no catalogue menu; their code is <c>PLATFORM</c> + the view folder under
/// <c>Views/Platform/</c> (<c>/Views/Platform/Tenants/Index.cshtml</c> → <c>PLATFORMTENANTS</c>).</para>
///
/// <para>Codes are always in <see cref="NavNameLocalizer.Normalize"/> form, the one transform the resx keys use.</para>
/// </summary>
public static class CurrentPage
{
    public const string PageCodeItem = "Diten.PageCode";
    public const string MenuPagesItem = "Diten.MenuPages";
    public const string AccessDeniedItem = "Diten.AccessDenied";
    public const string PageCodeViewData = "PageCode";
    public const string PlatformPrefix = "PLATFORM";

    /// <summary>The normalized code of the page whose route is the longest segment-boundary prefix of <paramref name="requestPath"/>.</summary>
    public static string? Match(string? requestPath, IEnumerable<(string? PageCode, string? RoutePath)> pages)
    {
        if (string.IsNullOrWhiteSpace(requestPath))
        {
            return null;
        }

        var path = requestPath.TrimEnd('/');
        string? best = null;
        var bestLength = -1;
        foreach (var (pageCode, routePath) in pages)
        {
            if (string.IsNullOrWhiteSpace(pageCode) || string.IsNullOrWhiteSpace(routePath) || !routePath.StartsWith('/'))
            {
                continue;
            }

            var route = routePath.Split('?', '#')[0].TrimEnd('/');
            if (route.Length == 0)
            {
                continue;
            }

            var matches = path.Equals(route, StringComparison.OrdinalIgnoreCase)
                || (path.StartsWith(route, StringComparison.OrdinalIgnoreCase) && path[route.Length] == '/');
            if (matches && route.Length > bestLength)
            {
                best = pageCode;
                bestLength = route.Length;
            }
        }

        var normalized = best is null ? null : NavNameLocalizer.Normalize(best);
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }

    /// <summary>The platform admin code for a view path, or null when the view is not under <c>Views/Platform/</c>.</summary>
    public static string? ForPlatformView(string? viewPath)
    {
        const string marker = "/Views/Platform/";
        if (string.IsNullOrWhiteSpace(viewPath))
        {
            return null;
        }

        var index = viewPath.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return null;
        }

        var rest = viewPath[(index + marker.Length)..];
        var slash = rest.IndexOf('/');
        if (slash <= 0)
        {
            return null;
        }

        var folder = NavNameLocalizer.Normalize(rest[..slash]);
        return folder.Length == 0 ? null : PlatformPrefix + folder;
    }

    /// <summary>The code the shell uses for this request: an explicit <c>ViewData["PageCode"]</c>, else the menu's match.</summary>
    public static string? Resolve(HttpContext? context, ViewDataDictionary? viewData)
    {
        if (viewData?[PageCodeViewData] is string explicitCode && !string.IsNullOrWhiteSpace(explicitCode))
        {
            var normalized = NavNameLocalizer.Normalize(explicitCode);
            return normalized.Length == 0 ? null : normalized;
        }

        return context?.Items[PageCodeItem] as string;
    }

    /// <summary>
    /// WP-UX-HELP-PANEL-02 — the menu's pages (code → address, required permission), recorded by the menu component
    /// with the page code, so the help panel's "Related pages" links where the menu links and to nothing it does not hold.
    /// Empty when the menu did not load: the panel then draws no related card.
    /// </summary>
    public static IReadOnlyDictionary<string, MenuPage> MenuPages(HttpContext? context) =>
        context?.Items[MenuPagesItem] as IReadOnlyDictionary<string, MenuPage> ?? EmptyMenu;

    /// <summary>Keeps the first entry per normalized code; an entry without a code or a route is not a page.</summary>
    public static void RecordMenuPages(HttpContext? context, IEnumerable<MenuPage> pages)
    {
        if (context is null)
        {
            return;
        }

        var byCode = new Dictionary<string, MenuPage>(StringComparer.Ordinal);
        foreach (var page in pages)
        {
            var code = NavNameLocalizer.Normalize(page.Code ?? string.Empty);
            if (code.Length > 0 && !string.IsNullOrWhiteSpace(page.RoutePath) && page.RoutePath.StartsWith('/'))
            {
                byCode.TryAdd(code, page with { Code = code });
            }
        }

        context.Items[MenuPagesItem] = byCode;
    }

    private static readonly IReadOnlyDictionary<string, MenuPage> EmptyMenu = new Dictionary<string, MenuPage>();

    /// <summary>Set by <c>_AccessDenied.cshtml</c>: the body drew the unauthorized face, so the shell draws no page help.</summary>
    public static bool IsAccessDenied(HttpContext? context) =>
        context?.Items[AccessDeniedItem] is true;

    public static void MarkAccessDenied(HttpContext? context)
    {
        if (context is not null)
        {
            context.Items[AccessDeniedItem] = true;
        }
    }
}

/// <summary>One page of the tenant menu: its normalized code, its address and the permission that shows it.</summary>
public sealed record MenuPage(string Code, string RoutePath, string? RequiredPermission);
