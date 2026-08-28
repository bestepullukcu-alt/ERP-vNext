namespace Diten.CrmService.Application.Features.Campaign.Read;

/// <summary>
/// MOD-0165 FU10 — the NARROW, read-only window onto MOD-0167 Segment that campaign targeting needs, and nothing else.
///
/// <para><b>Why a seam instead of the segment repository.</b> <c>ISegmentRepository</c> carries <c>InsertAsync</c> and
/// <c>ReplaceAsync</c>. Handing that to a campaign handler would put a write path into another module's aggregate one
/// keystroke away, and no code review reliably catches that twice a year. This interface cannot write, so the
/// boundary is structural rather than a promise — the same reason the cycle period defines its own Territory window
/// instead of taking Territory's repository.</para>
///
/// <para><b>It lives on the CONSUMER side deliberately.</b> Defining it under <c>Features/Segmentation</c> would mean
/// designing MOD-0167's consumption contract on its behalf, and that is its own module's decision. Folding both into
/// a canonical segment reader later is a documented follow-up.</para>
///
/// <para><b>Read-only, in-process, tenant-scoped.</b> No method here writes, none holds an <c>HttpClient</c>, and the
/// tenant comes from the request context — a campaign can never reach another tenant's segments through it.</para>
/// </summary>
public interface ICampaignSegmentCatalog
{
    /// <summary>
    /// Several segments by id, in ONE round trip. Used by the write path to prove a targeting change and by the read
    /// path to project labels; reading them one at a time would be an N+1 over a campaign list.
    /// <para>Ids that do not exist in the caller's tenant are simply absent from the result — this is a lookup, and
    /// the write path is where a missing reference is refused. Order is not guaranteed; callers index by id.</para>
    /// </summary>
    Task<IReadOnlyList<CampaignSegmentRef>> GetByIdsAsync(
        IReadOnlyCollection<Guid> segmentIds,
        CancellationToken cancellationToken);

    /// <summary>
    /// The segments a picker may offer: ACTIVE ones, so the UI never shows something the write path would refuse.
    /// A superseded-but-still-active segment is included — whether to move to a newer version is the author's call,
    /// and hiding the current one would silently unbind campaigns that already point at it.
    /// </summary>
    Task<IReadOnlyList<CampaignSegmentRef>> ListSelectableAsync(CancellationToken cancellationToken);
}

/// <summary>
/// What a campaign may know about a segment: enough to validate a link and to label it on screen, and no more.
/// <para>A campaign stores the ID and re-reads. Copying the code, the name or the subject type into its own document
/// would go stale the moment the segment is renamed — the same rule the cycle-period projection follows.</para>
/// </summary>
public sealed record CampaignSegmentRef(
    Guid SegmentId,
    string SegmentCode,
    string SegmentName,
    string SubjectType,
    string SegmentStatus,
    bool Superseded,
    Guid VersionLineageId,
    int SegmentVersion);
