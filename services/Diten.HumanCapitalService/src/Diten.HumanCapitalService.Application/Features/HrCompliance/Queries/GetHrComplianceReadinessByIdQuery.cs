using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.HrCompliance.Queries;

public sealed record GetHrComplianceReadinessByIdQuery(Guid Id) : IRequest<Response<HrComplianceReadinessDto>>;
