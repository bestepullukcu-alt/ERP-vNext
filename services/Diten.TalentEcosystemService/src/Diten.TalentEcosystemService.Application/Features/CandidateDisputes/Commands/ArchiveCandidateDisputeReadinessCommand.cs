using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.CandidateDisputes.Commands;

public sealed record ArchiveCandidateDisputeReadinessCommand(Guid Id) : IRequest<Response<NoContent>>;
