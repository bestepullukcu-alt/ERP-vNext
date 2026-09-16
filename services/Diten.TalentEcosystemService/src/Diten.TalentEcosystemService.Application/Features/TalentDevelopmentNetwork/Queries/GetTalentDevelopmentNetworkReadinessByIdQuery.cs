using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.TalentDevelopmentNetwork.Queries;

public sealed record GetTalentDevelopmentNetworkReadinessByIdQuery(Guid Id) : IRequest<Response<TalentDevelopmentNetworkReadinessDto>>;
