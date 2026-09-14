using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.ReferenceExchange.Queries;

public sealed record GetReferenceExchangeReadinessByIdQuery(Guid Id) : IRequest<Response<ReferenceExchangeReadinessDto>>;
