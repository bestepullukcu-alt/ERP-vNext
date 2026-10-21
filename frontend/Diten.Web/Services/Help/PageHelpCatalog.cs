using System.Text.RegularExpressions;
using Diten.Web.Services.Navigation;
using Microsoft.AspNetCore.Html;

namespace Diten.Web.Services.Help;

/// <summary>
/// WP-UX-HELP-PANEL-02 — what the "?" help panel needs that is NOT language: which pages relate to which (and the icon
/// of each link), and the icon of each step. One place, keyed by the stable page code (<c>Nav.Page.{CODE}</c>); the
/// words stay in <c>PageHelpResource</c>.
/// </summary>
public static class PageHelpCatalog
{
    /// <summary>The icon a step gets when the catalog names none for it.</summary>
    public const string DefaultStepIcon = "bx-right-arrow-circle";

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<RelatedPage>> Related =
        new Dictionary<string, IReadOnlyList<RelatedPage>>(StringComparer.Ordinal)
        {
            ["USERS"] = [new("USERROLES", "bx-shield-quarter"), new("ROLES", "bx-id-card")],
            ["ROLES"] = [new("ROLEPERMISSIONS", "bx-key"), new("USERROLES", "bx-shield-quarter")],
            ["ROLEPERMISSIONS"] = [new("ROLES", "bx-id-card"), new("USERROLES", "bx-shield-quarter")],
            ["USERROLES"] = [new("USERS", "bx-user"), new("ROLES", "bx-id-card")],
        };

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> StepIcons =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["USERS"] = ["bx-filter-alt", "bx-user-plus", "bx-envelope", "bx-power-off", "bx-shield-quarter"],
            ["ROLES"] = ["bx-filter-alt", "bx-plus-circle", "bx-key", "bx-lock-alt"],
            ["ROLEPERMISSIONS"] = ["bx-id-card", "bx-filter-alt", "bx-check-square", "bx-x-circle", "bx-bar-chart-alt-2"],
            ["USERROLES"] = ["bx-user", "bx-filter-alt", "bx-user-check", "bx-user-minus"],
            ["ITEMS"] = ["bx-filter-alt", "bx-plus-circle", "bx-send", "bx-transfer-alt", "bx-help-circle"],
            // WP-SYNC-144 W-UXH — the CRM tenant pages of W3 / W4 / W3E (BL-642 + the two W3E pages)
            ["VISITEVIDENCESETTINGS"] = ["bx-layer", "bx-slider-alt", "bx-history", "bx-time-five", "bx-task"],
            ["CRMVISITEVIDENCEFLAGS"] = ["bx-calendar", "bx-filter-alt", "bx-bar-chart-alt-2", "bx-show", "bx-group"],
            ["CRMVISITQRCODES"] = ["bx-filter-alt", "bx-qr", "bx-printer", "bx-block", "bx-slider-alt"],
            // WP-VW-W5 W5-WEB — the sent visit e-mails
            ["CRMVISITNOTIFICATIONS"] = ["bx-calendar", "bx-filter-alt", "bx-user-check", "bx-envelope-open", "bx-cog"],
            // WP-VW-W6 W6-WEB-a — the pharmacy pre-orders (COM-PREORDER-LIST / -FORM / -APPROVE / -SETTINGS)
            ["PREORDERS"] = ["bx-filter-alt", "bx-money", "bx-show", "bx-package"],
            ["COMPREORDERFORM"] = ["bx-capsule", "bx-error", "bx-pen", "bx-send"],
            ["COMPREORDERAPPROVE"] = ["bx-help-circle", "bx-bar-chart-alt-2", "bx-check-shield", "bx-link"],
            ["PREORDERSETTINGS"] = ["bx-user-check", "bx-time-five", "bx-purchase-tag-alt", "bx-hash"],
            ["CRMDOCTORINSIGHTTHRESHOLDS"] = ["bx-table", "bx-edit-alt", "bx-check-circle", "bx-plus-circle", "bx-history"],
            ["OBJECTIONRESPONSES"] = ["bx-filter-alt", "bx-plus-circle", "bx-send", "bx-check-shield", "bx-archive"],
            ["COMPETITORPRODUCTS"] = ["bx-filter-alt", "bx-plus-circle", "bx-user-voice", "bx-task", "bx-git-merge"],
            ["VISITWORKSPACE"] = ["bx-calendar-week", "bx-check-double", "bx-play-circle", "bx-send", "bx-calendar-x"],
        };

    /// <summary>The icon of step <paramref name="step"/> (1-based) of a page, or <see cref="DefaultStepIcon"/>.</summary>
    public static string StepIcon(string? pageCode, int step) =>
        pageCode is not null && StepIcons.TryGetValue(pageCode, out var icons) && step >= 1 && step <= icons.Count
            ? icons[step - 1]
            : DefaultStepIcon;

    /// <summary>
    /// The related pages the reader can open: listed for this page, in the menu (so the address is the menu's), and
    /// either free or held (<paramref name="holds"/> — the same rule as the menu). Catalog order.
    /// </summary>
    public static IReadOnlyList<RelatedLink> RelatedFor(
        string? pageCode, IReadOnlyDictionary<string, MenuPage> menu, Func<string, bool> holds)
    {
        if (pageCode is null || !Related.TryGetValue(pageCode, out var related))
        {
            return [];
        }

        return related
            .Where(r => menu.TryGetValue(r.Code, out var page)
                && (string.IsNullOrWhiteSpace(page.RequiredPermission) || holds(page.RequiredPermission!)))
            .Select(r => new RelatedLink(r.Code, menu[r.Code].RoutePath, r.Icon))
            .ToList();
    }

    /// <summary>The codes a page relates to, whether or not the reader can open them (for the resx guard).</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> RelatedCodes =>
        Related.ToDictionary(p => p.Key, p => (IReadOnlyList<string>)p.Value.Select(r => r.Code).ToList(), StringComparer.Ordinal);

    // A button or field name in the text: “…” (en, tr, zh) or «…» / « … » (fr, es, ar, ru).
    private static readonly Regex Quoted = new("“([^”]{1,80})”|«\\u00A0?\\s?([^»]{1,80}?)\\s?\\u00A0?»", RegexOptions.CultureInvariant);

    /// <summary>
    /// A step's text with every quoted button / field name drawn as a small label. Every piece of the resx text is
    /// HTML-encoded on write (it is never markup, a "&lt;script&gt;" stays text); only the label span is this method's.
    /// </summary>
    public static IHtmlContent Labelled(string? text)
    {
        var builder = new HtmlContentBuilder();
        if (string.IsNullOrEmpty(text))
        {
            return builder;
        }

        var at = 0;
        foreach (Match match in Quoted.Matches(text))
        {
            builder.Append(text[at..match.Index]);
            builder.AppendHtml("<span class=\"diten-help-ui\">");
            builder.Append(match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value);
            builder.AppendHtml("</span>");
            at = match.Index + match.Length;
        }

        builder.Append(text[at..]);
        return builder;
    }
}

/// <summary>A page another page's help points to, and the icon of that link.</summary>
public sealed record RelatedPage(string Code, string Icon);

/// <summary>A related page the reader can open: its code (the name is <c>Nav.Page.{Code}</c>), its address, its icon.</summary>
public sealed record RelatedLink(string Code, string RoutePath, string Icon);
