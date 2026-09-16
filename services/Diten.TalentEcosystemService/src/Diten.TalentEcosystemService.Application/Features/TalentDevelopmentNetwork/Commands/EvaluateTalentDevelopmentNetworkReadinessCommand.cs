using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.TalentDevelopmentNetwork.Commands;

public sealed record EvaluateTalentDevelopmentNetworkReadinessCommand(Guid Id) : IRequest<Response<TalentDevelopmentNetworkReadinessDto>>;
