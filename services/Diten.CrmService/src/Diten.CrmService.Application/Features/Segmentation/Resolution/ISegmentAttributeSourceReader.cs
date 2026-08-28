using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.Segmentation.Resolution;

/// <summary>
/// MOD-0167 FU02 attribute-source orchestrator. Given a segment and its candidate set it performs Phase 1.5 and Phase 2
/// with <b>one bulk read per source</b> (links, account attributes, territory coverage, consent, concept affinity) and
/// hands back everything the evaluator needs.
/// <para>A source is consulted only when the criteria tree actually uses it, and each derived set is computed ONCE and
/// applied to every candidate. That is why cost grows with the number of SOURCES, not with the number of candidates.</para>
/// </summary>
public interface ISegmentAttributeSourceReader
{
    Task<SegmentAttributeContext> LoadAsync(
        Guid tenantId,
        Segment segment,
        IReadOnlyList<SegmentSubjectSnapshot> candidates,
        DateTimeOffset effectiveAt,
        CancellationToken cancellationToken);
}
