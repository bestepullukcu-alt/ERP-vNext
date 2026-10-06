using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Segmentation.Queries;

/// <summary>
/// "If I saved this rule right now, who would it reach?" — the live reach preview for a DRAFT (unsaved) segment rule.
/// It carries no <c>SegmentId</c>: the criteria tree travels in the body, exactly as it sits in the editor, and NOTHING
/// is persisted (no segment, no membership row, no snapshot, no cache).
/// <para>It is the same read that <c>/resolve</c> performs, aimed at an in-memory draft instead of a stored segment:
/// <see cref="SegmentReachPreviewDto.TotalCount"/> is the count the same rule would resolve to once saved,
/// <see cref="SegmentReachPreviewDto.ConditionCounts"/> is each predicate counted ALONE ("N match this by itself"), and
/// <see cref="SegmentReachPreviewDto.SampleMembers"/> is a bounded sample. Member identity is PII, so this rides the
/// same <c>crm.segment.resolve</c> key as <c>/resolve</c>.</para>
/// </summary>
public sealed record PreviewSegmentReachQuery(
    string SubjectType,
    string MatchMode,
    IReadOnlyList<SegmentCriteriaNodeInput>? Criteria,
    DateTimeOffset? EffectiveAt) : IRequest<Response<SegmentReachPreviewDto>>;
