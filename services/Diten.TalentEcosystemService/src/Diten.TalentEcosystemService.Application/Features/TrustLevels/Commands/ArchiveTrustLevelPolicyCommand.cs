using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.TrustLevels.Commands;

public sealed record ArchiveTrustLevelPolicyCommand(Guid Id) : IRequest<Response<NoContent>>;
