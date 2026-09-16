using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.CandidateDisputes.Queries;

public sealed record GetCandidateDisputeReadinessListQuery : IRequest<Response<IReadOnlyList<CandidateDisputeReadinessListItemDto>>>;
