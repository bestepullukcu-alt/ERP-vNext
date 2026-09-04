using Diten.CrmService.Application.Features.VisitContentSequence;

namespace Diten.CrmService.Api.Models.CRM;

/// <summary>
/// MOD-0155 FU04 read-only preview request body — the wire shape of the <see cref="VisitContentSequenceRequest"/>
/// context (pack §4 / §14). <c>TenantId</c> is absent by construction: the resolver reads tenant-scoped seams through
/// <c>ITenantContext</c> and persists nothing. <c>PriorStageIndex</c> is the ordinal of the doctor's LAST visit stage
/// (from FU01's stored <c>PlannedVisitContentRef.StageIndex</c>); omit it for a first visit, and the resolver starts at
/// index 0.
/// </summary>
public sealed class VisitContentPreviewRequest
{
    public string SubjectType { get; set; } = string.Empty;
    public Guid SubjectId { get; set; }
    public Guid? SegmentId { get; set; }
    public Guid? StrategyTemplateId { get; set; }
    public Guid? CyclePeriodId { get; set; }
    public int? PriorStageIndex { get; set; }
    public DateTimeOffset? EffectiveAt { get; set; }

    public VisitContentSequenceRequest ToRequest()
        => new(
            SubjectType ?? string.Empty,
            SubjectId,
            SegmentId,
            StrategyTemplateId,
            CyclePeriodId,
            PriorStageIndex,
            EffectiveAt);
}
