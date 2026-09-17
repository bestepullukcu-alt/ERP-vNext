using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.ReferenceExchange.Commands;

public sealed record CreateReferenceExchangeReadinessCommand(ReferenceExchangeReadinessRequest Request) : IRequest<Response<Guid>>;
