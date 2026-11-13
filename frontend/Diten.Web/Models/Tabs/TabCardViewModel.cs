namespace Diten.Web.Models.Tabs;

/// <summary>
/// K-TABS (owner decision, 2026-10-15) — the tab strip every tabbed page uses: the Work Center's tab card, as a used
/// component instead of a copied one. <c>Views/Shared/Components/Tabs/_TabCard.cshtml</c> renders it with the Work Center's
/// classes one for one (<c>wcn-tabcard</c> · <c>wcn-tabs</c> · <c>wc-tab-compact</c> · <c>wc-tab-icon</c> ·
/// <c>wcn-tab-count</c>, all already in the shared backbone-custom.css) and <c>assets/js/diten-tabs.js</c> gives it its
/// behaviour (click, arrows / Home / End, RTL, the <c>diten:tab-change</c> event). The Work Center keeps its own
/// JavaScript-drawn strip (WorkCenterNext/app.js); this is the same markup for server-rendered pages.
/// <para>The card produces NO text of its own: every label arrives already localized by the page.</para>
/// </summary>
public sealed class TabCardViewModel
{
    /// <summary>The tabs, in order. Exactly one should be <see cref="TabCardItem.Active"/> (the first one is otherwise).</summary>
    public IReadOnlyList<TabCardItem> Tabs { get; init; } = [];

    /// <summary>The tablist's accessible name (already localized).</summary>
    public string? AriaLabel { get; init; }

    /// <summary>The id of the optional tools area at the inline end of the card (<c>div.d-flex.align-items-center.gap-2.ms-auto</c>);
    /// null = no tools area.</summary>
    public string? ToolsId { get; init; }

    /// <summary>The tab the strip opens on: the one marked active, else the first.</summary>
    public TabCardItem? ActiveTab => Tabs.FirstOrDefault(t => t.Active) ?? Tabs.FirstOrDefault();
}

public sealed class TabCardItem
{
    /// <summary>The page's own name for the tab (<c>data-tab-key</c>, carried by the <c>diten:tab-change</c> event).</summary>
    public required string Key { get; init; }

    /// <summary>The button's id.</summary>
    public required string Id { get; init; }

    /// <summary>The boxicons glyph class (<c>bx-list-ul</c>). Shown at every width; the text only from md up.</summary>
    public required string Icon { get; init; }

    /// <summary>The already-localized label. Below md only the icon is visible, so it is also the button's aria-label + title.</summary>
    public required string Text { get; init; }

    /// <summary>The red corner count (work waiting in the tab); null or 0 = no badge.</summary>
    public int? Count { get; init; }

    public bool Active { get; init; }

    /// <summary>The id of the panel the tab shows (<c>aria-controls</c>).</summary>
    public required string Controls { get; init; }
}
