using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Segmentation.Queries;

/// <summary>The single-subject question the MOD-0167-FU01 seam asks. Answers member / not-member / unknown with reason
/// codes; <c>unknown</c> is an answer and is never <c>member</c>.</summary>
public sealed record EvaluateSegmentMembershipQuery(
    Guid SegmentId,
    string SubjectType,
    Guid SubjectId,
    DateTimeOffset? EffectiveAt) : IRequest<Response<SegmentMembershipVerdictDto>>;
