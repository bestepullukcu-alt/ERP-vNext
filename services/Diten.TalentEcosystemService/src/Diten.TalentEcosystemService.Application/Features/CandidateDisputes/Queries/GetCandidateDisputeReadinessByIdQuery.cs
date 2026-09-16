using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.CandidateDisputes.Queries;

public sealed record GetCandidateDisputeReadinessByIdQuery(Guid Id) : IRequest<Response<CandidateDisputeReadinessDto>>;
