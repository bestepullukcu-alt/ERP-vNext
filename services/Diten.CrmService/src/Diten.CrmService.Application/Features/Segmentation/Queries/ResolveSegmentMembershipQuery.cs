using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Segmentation.Queries;

/// <summary>
/// "Who is in this segment right now?" — a pure READ that persists nothing: no membership row, no snapshot, no cache,
/// no usage log. It is deterministic for unchanged source data, bounded by the published candidate ceiling, and it
/// returns every eliminated candidate with its reason when asked, so nothing ever drops out silently.
/// </summary>
public sealed record ResolveSegmentMembershipQuery(
    Guid SegmentId,
    DateTimeOffset? EffectiveAt,
    int? Limit,
    int? Offset,
    bool IncludeExcluded) : IRequest<Response<SegmentResolutionResultDto>>;
