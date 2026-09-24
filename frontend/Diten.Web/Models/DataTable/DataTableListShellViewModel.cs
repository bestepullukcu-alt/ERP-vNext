namespace Diten.Web.Models.DataTable;

/// <summary>
/// The table card every list screen used to copy by hand (BL-440, measured 2026-09-23: 138 copies, 26 with a
/// selection column, 6 with the shared skeleton, one screen whose JS bound bulk selection into a column its markup
/// never drew). A screen now describes the card and <c>_ListShell.cshtml</c> renders it — the same pattern as
/// <see cref="DataTableBulkActionBarViewModel"/> and <c>_BulkActionBar.cshtml</c>.
///
/// <para>The shell produces NO text of its own: every header arrives already localized by the page, so the
/// component owes no resx key in any language.</para>
/// </summary>
public sealed class DataTableListShellViewModel
{
    /// <summary>The two data modes a list may declare (frontend-datatable-template.md, "Veri modeli").</summary>
    public static readonly IReadOnlyList<string> AllowedDataModes = ["server", "client"];

    /// <summary>The <c>&lt;table id&gt;</c>: the storage key of every persisted view, so it is never optional.</summary>
    public required string TableId { get; init; }

    /// <summary>
    /// <c>"server"</c> or <c>"client"</c>, rendered as <c>data-dt-data-mode</c>. Anything else throws at render:
    /// a list with an undeclared or misspelt data mode is what the verifier's data_mode rule exists to catch.
    /// </summary>
    public required string DataMode { get; init; }

    /// <summary>Rendered as the <c>datatables-{slug}</c> class the page's JS and CSS select on.</summary>
    public string Slug { get; init; } = string.Empty;

    /// <summary>The data columns, in order. Headers are already-localized text from the page.</summary>
    public IReadOnlyList<DataTableColumnViewModel> Columns { get; init; } = [];

    /// <summary>Draws the select-all checkbox column (the thing the bulk bar selects into).</summary>
    public bool HasSelection { get; init; }

    /// <summary>Header text of the actions column; <c>null</c> means no actions column at all.</summary>
    public string? ActionsHeader { get; init; }

    public bool HasActions => ActionsHeader is not null;

    public bool IsDataModeValid => AllowedDataModes.Contains(DataMode, StringComparer.Ordinal);
}

public sealed class DataTableColumnViewModel
{
    /// <summary>Already-localized header text (the page's <c>Localizer["Key"].Value</c>).</summary>
    public string Header { get; init; } = string.Empty;

    /// <summary>Optional <c>&lt;th&gt;</c> class, e.g. <c>all</c> (always visible) or <c>cell-fit</c>.</summary>
    public string? CssClass { get; init; }
}
