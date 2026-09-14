using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.OfferManagement.Queries;

public sealed record GetOfferAuditMetadataQuery(Guid Id) : IRequest<Response<OfferAuditMetadataDto>>;
