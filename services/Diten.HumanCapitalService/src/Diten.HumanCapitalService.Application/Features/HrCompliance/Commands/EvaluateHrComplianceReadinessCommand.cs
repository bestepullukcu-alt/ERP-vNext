using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.HrCompliance.Commands;

public sealed record EvaluateHrComplianceReadinessCommand(Guid Id) : IRequest<Response<HrComplianceReadinessDto>>;
