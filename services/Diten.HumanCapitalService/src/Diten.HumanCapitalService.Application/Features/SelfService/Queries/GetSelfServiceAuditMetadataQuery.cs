using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.SelfService.Queries;

public sealed record GetSelfServiceAuditMetadataQuery(Guid Id) : IRequest<Response<SelfServiceAuditMetadataDto>>;
