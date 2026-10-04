using Microsoft.AspNetCore.Mvc.Localization;

namespace Diten.Web.Services.Navigation;

/// <summary>
/// BL-520 — the top-bar search box's label, handed by every shell layout to main.js as <c>data-search-placeholder</c>
/// (main.js is a static asset with no access to the resx; it fills the platform's shortcut name into <c>{0}</c>).
///
/// <para><b>Why a helper and not <c>@SharedLocalizer["ShellSearchPlaceholder"]</c> in the layout.</b> A missing resource
/// does not fail: the localizer returns the KEY as its value, and the search box would read "ShellSearchPlaceholder" —
/// worse than the English it replaced, and the same in every language. This returns <c>null</c> instead, which Razor
/// renders as no attribute at all, and main.js then shows the shortcut alone. The same guard as
/// <c>_DataTableL10n.cshtml</c>; one place, so the three layouts (tenant shell, platform shell, the default
/// <c>_Layout</c>) cannot drift apart.</para>
/// </summary>
public static class ShellSearchLabel
{
    public const string ResourceKey = "ShellSearchPlaceholder";

    public static LocalizedHtmlString? Template(IHtmlLocalizer localizer)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        var localized = localizer[ResourceKey];
        return localized.IsResourceNotFound || string.IsNullOrWhiteSpace(localized.Value) ? null : localized;
    }
}
