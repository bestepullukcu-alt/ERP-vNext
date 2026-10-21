namespace Diten.Web.Models.Commercial;

/// <summary>
/// WP-VW-W6 (W6-WEB-b) — what screen D (MOD-0156: price lists · MF / discount rules · rule trial · change history · FX
/// rates) may draw for the caller (MOD-0156 §14). The CRM decides every write again; these flags only decide what is
/// RENDERED (UAS-001: without the screen's key only the notice — no skeleton, no redirect). A tab exists only for the
/// caller who may open it.
/// </summary>
public sealed class PricingPageViewModel
{
    public bool CanReadLists { get; set; }
    public bool CanCreateList { get; set; }
    public bool CanUpdateList { get; set; }
    public bool CanImportList { get; set; }
    public bool CanSubmitList { get; set; }
    public bool CanPublishList { get; set; }
    public bool CanExportLists { get; set; }

    public bool CanReadRules { get; set; }
    public bool CanCreateRule { get; set; }
    public bool CanUpdateRule { get; set; }
    public bool CanSubmitRule { get; set; }
    public bool CanPublishRule { get; set; }

    public bool CanReadFx { get; set; }
    public bool CanCreateFx { get; set; }
    public bool CanUpdateFx { get; set; }

    /// <summary>Any of the three read keys (the change history shows what each covers).</summary>
    public bool CanReadHistory => CanReadLists || CanReadRules || CanReadFx;

    /// <summary>The tab this page is (<c>lists</c> · <c>rules</c> · <c>trial</c> · <c>history</c> · <c>fx</c>).</summary>
    public string Tab { get; set; } = "lists";

    /// <summary>The page inside the tab (<c>index</c> · <c>create</c> · <c>edit</c> · <c>details</c> · <c>import</c>).</summary>
    public string Screen { get; set; } = "index";

    /// <summary>The record of a per-record page (a list, an import, a rule, a rate); null otherwise.</summary>
    public Guid? Id { get; set; }

    /// <summary>Whether THIS page may be drawn (the flag its guard reads).</summary>
    public bool Allowed { get; set; }
}
