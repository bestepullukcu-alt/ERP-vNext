namespace Diten.Web.Models.CRM;

/// <summary>
/// MOD-0165 FU10 — a read-time projection of one targeted segment, for display only. Never posted back and never
/// persisted: the campaign stores the id, so a renamed segment shows its new name on the next read.
///
/// <para><b>It lives in its own file deliberately.</b> The DataTable contract verifier resolves a form field's type by
/// the LAST property of that name in the file; a non-form view model parked beside the edit model can shadow a form
/// field and make a rule report a defect that does not exist. That happened once during FU08 and is not repeated.</para>
///
/// <para><see cref="IsResolvable"/> is false when the segment could not be read at all — the screen then shows the
/// pinned id rather than a label nobody can vouch for.</para>
/// </summary>
public sealed class CampaignTargetedSegmentViewModel
{
    public Guid SegmentId { get; set; }

    public DateTimeOffset LinkedAt { get; set; }

    public bool IsResolvable { get; set; }

    public string? SegmentCode { get; set; }

    public string? SegmentName { get; set; }

    public string? SubjectType { get; set; }

    public string? SegmentStatus { get; set; }

    /// <summary>True when a newer version of this segment exists. Surfaced, never acted on — moving is the author's
    /// deliberate decision.</summary>
    public bool Superseded { get; set; }

    public int? SegmentVersion { get; set; }
}
