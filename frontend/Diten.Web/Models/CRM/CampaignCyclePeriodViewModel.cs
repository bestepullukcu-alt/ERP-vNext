namespace Diten.Web.Models.CRM;

/// <summary>
/// MOD-0165 FU08 — the read-time projection of a bound cycle period, for display only. It is never posted back and
/// never persisted: the campaign stores the id alone, so a renamed period shows its new name on the next read.
///
/// <para><b>It lives in its own file deliberately.</b> It carries <c>StartDate</c> / <c>EndDate</c> of its own, and
/// the DataTable contract verifier resolves a form field's type by the LAST property of that name in the file. Kept
/// beside <c>CampaignEditViewModel</c>, this type's non-nullable <c>EndDate</c> would shadow the form's nullable one
/// and the optional-date rule would report a defect that does not exist.</para>
/// </summary>
public sealed class CampaignCyclePeriodViewModel
{
    public Guid CyclePeriodId { get; set; }
    public string CycleCode { get; set; } = string.Empty;
    public string CycleName { get; set; } = string.Empty;
    public int Year { get; set; }
    public int SequenceInYear { get; set; }
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset EndDate { get; set; }
    public string CycleStatus { get; set; } = string.Empty;
}
