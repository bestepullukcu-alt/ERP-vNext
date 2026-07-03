using Diten.Web.Navigation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

// FEAT-CTRLK-PLATFORM-DYNAMIC — server-driven Ctrl+K for the platform-admin shell, the counterpart of
// TenantSearchController. It projects the single canonical PlatformNavigationCatalog into the SAME search schema
// ({ navigation: { "<Section>": [ {name,url,group,icon,keywords} ] }, suggestions }) and localizes labels server-side
// via SharedResource (per request culture), so the three static platform-search.{culture}.json files are no longer
// the source of truth (they remain only as a JS fallback). Create-route screens contribute a "Create X" shortcut.
// Route lives under /Platform so the global ShellAccessFilter sees isPlatformPath=true (StartsWithSegments("/platform"))
// and admits platform_admin/partner_admin actors — a top-level /PlatformSearch is NOT segment-matched → 403.
[Authorize]
[Route("Platform/Search")]
public sealed class PlatformSearchController : Controller
{
    private const int SuggestionsCap = 12;

    private readonly IStringLocalizer<SharedResource> _localizer;

    public PlatformSearchController(IStringLocalizer<SharedResource> localizer)
    {
        _localizer = localizer;
    }

    [HttpGet("data")]
    public IActionResult Data()
    {
        var section = _localizer[PlatformNavigationCatalog.SectionResourceKey].Value;
        var items = new List<SearchItem>();

        foreach (var nav in PlatformNavigationCatalog.Items)
        {
            var label = _localizer[nav.LabelResourceKey].Value;
            items.Add(new SearchItem(label, nav.Url, section, nav.Icon, Keywords(nav, label)));

            // Create shortcut — only for screens that expose a standalone create route.
            if (!string.IsNullOrWhiteSpace(nav.CreateUrl) && !string.IsNullOrWhiteSpace(nav.CreateLabelResourceKey))
            {
                items.Add(new SearchItem(
                    _localizer[nav.CreateLabelResourceKey].Value, nav.CreateUrl!, section, "bx-plus-circle",
                    CreateKeywords(nav, label)));
            }
        }

        var navigation = new Dictionary<string, List<SearchItem>>(StringComparer.Ordinal) { [section] = items };
        var suggestions = BuildSuggestions(navigation, SuggestionsCap);

        // Short private cache — the catalog is static per culture, but the labels are per-request-localized.
        Response.Headers.CacheControl = "private, max-age=60";
        return Json(new { navigation, suggestions });
    }

    private static IReadOnlyList<string> Keywords(PlatformNavItem nav, string label)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { nav.Key, label };
        if (nav.Keywords is not null)
        {
            foreach (var k in nav.Keywords) set.Add(k);
        }
        // Fold the (localized) label's word tokens so a Turkish/English label term matches too.
        foreach (var token in label.Split(new[] { ' ', '-', '_', '/' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.Length > 1) set.Add(token);
        }
        return set.ToList();
    }

    private static IReadOnlyList<string> CreateKeywords(PlatformNavItem nav, string label)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "create", "add", "new", "yeni", "ekle", nav.Key };
        foreach (var token in label.Split(new[] { ' ', '-', '_', '/' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.Length > 1) set.Add(token);
        }
        return set.ToList();
    }

    // Section-grouped quick-nav palette shown before the user types; capped to keep the open-state compact.
    private static IReadOnlyDictionary<string, List<SearchItem>> BuildSuggestions(
        IReadOnlyDictionary<string, List<SearchItem>> navigation, int cap)
    {
        var suggestions = new Dictionary<string, List<SearchItem>>(StringComparer.Ordinal);
        var remaining = cap;
        foreach (var (section, items) in navigation)
        {
            if (remaining <= 0) break;
            var take = items.Take(remaining).ToList();
            if (take.Count == 0) continue;
            suggestions[section] = take;
            remaining -= take.Count;
        }

        return suggestions;
    }

    // Matches the platform-search.json / TenantSearchController item shape ({ name, url, group, icon, keywords }).
    private sealed record SearchItem(string Name, string Url, string Group, string Icon, IReadOnlyList<string> Keywords);
}
