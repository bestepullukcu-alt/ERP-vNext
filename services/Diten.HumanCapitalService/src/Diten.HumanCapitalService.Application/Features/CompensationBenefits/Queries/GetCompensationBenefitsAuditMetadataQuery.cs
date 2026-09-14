using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.CompensationBenefits.Queries;

public sealed record GetCompensationBenefitsAuditMetadataQuery(Guid Id) : IRequest<Response<CompensationBenefitsAuditMetadataDto>>;
