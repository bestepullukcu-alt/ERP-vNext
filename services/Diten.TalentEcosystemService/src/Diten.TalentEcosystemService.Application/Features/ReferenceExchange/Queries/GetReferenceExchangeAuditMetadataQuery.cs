using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.ReferenceExchange.Queries;

public sealed record GetReferenceExchangeAuditMetadataQuery(Guid Id) : IRequest<Response<ReferenceExchangeAuditMetadataDto>>;
