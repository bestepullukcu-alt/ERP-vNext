using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.ReferenceExchange.Commands;

public sealed record ArchiveReferenceExchangeReadinessCommand(Guid Id) : IRequest<Response<NoContent>>;
