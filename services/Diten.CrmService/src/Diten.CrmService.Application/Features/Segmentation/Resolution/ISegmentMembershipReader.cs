namespace Diten.CrmService.Application.Features.Segmentation.Resolution;

/// <summary>
/// MOD-0167 FU02 read-only consumption seam — the runtime answer to the MOD-0167-FU01 section 5 question
/// ("is this contact/account a member of that segment at that instant?") and the bounded resolve a MOD-0165 campaign
/// snapshot can consume later.
/// <para><b>It reports; it is not an engine.</b> It writes nothing, generates no CampaignTarget, and writes no
/// VisitFrequencyPolicy — those stay in MOD-0165, and this FU does not connect them (follow-up F-SNAPSHOT).</para>
/// <para><b><c>unknown</c> is an answer, not an error — and never <c>member</c>.</b> A consumer that cannot tell the
/// two apart must treat unknown as not-a-member. A consumer needs no raw segment or target-customer read permission:
/// it comes through here, so member identity (PII) stays behind the <c>crm.segment.resolve</c> key.</para>
/// </summary>
public interface ISegmentMembershipReader
{
    Task<SegmentMembershipVerdict> IsMemberAsync(
        Guid segmentId,
        string subjectType,
        Guid subjectId,
        DateTimeOffset effectiveAt,
        CancellationToken cancellationToken);

    Task<SegmentResolutionResult> ResolveAsync(
        Guid segmentId,
        DateTimeOffset effectiveAt,
        int limit,
        int offset,
        CancellationToken cancellationToken);
}
