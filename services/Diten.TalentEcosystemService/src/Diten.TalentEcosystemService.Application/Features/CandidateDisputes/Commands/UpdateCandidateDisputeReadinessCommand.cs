using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.CandidateDisputes.Commands;

public sealed record UpdateCandidateDisputeReadinessCommand(Guid Id, CandidateDisputeReadinessRequest Request) : IRequest<Response<NoContent>>;
