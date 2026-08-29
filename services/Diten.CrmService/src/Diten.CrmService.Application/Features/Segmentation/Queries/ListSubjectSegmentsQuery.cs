using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Segmentation.Queries;

/// <summary>The reverse question: which ACTIVE segments does this one subject belong to? Evaluated one candidate at a
/// time (there is no N-candidate scan here) and bounded by the published ceiling — past it the answer is a 422, never a
/// quietly shortened list.</summary>
public sealed record ListSubjectSegmentsQuery(
    string SubjectType,
    Guid SubjectId,
    DateTimeOffset? EffectiveAt) : IRequest<Response<SubjectSegmentListDto>>;
