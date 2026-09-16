using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.RehireRecommendations.Commands;

public sealed record ArchiveRehireRecommendationReadinessCommand(Guid Id) : IRequest<Response<NoContent>>;
