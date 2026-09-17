using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.ReferenceExchange.Commands;

public sealed record UpdateReferenceExchangeReadinessCommand(Guid Id, ReferenceExchangeReadinessRequest Request) : IRequest<Response<NoContent>>;
