using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.HrCompliance.Queries;

public sealed record GetHrComplianceAuditMetadataQuery(Guid Id) : IRequest<Response<HrComplianceAuditMetadataDto>>;
