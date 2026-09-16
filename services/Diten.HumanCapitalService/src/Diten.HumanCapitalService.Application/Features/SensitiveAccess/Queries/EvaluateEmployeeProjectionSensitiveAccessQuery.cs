using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.SensitiveAccess.Queries;

public sealed record EvaluateEmployeeProjectionSensitiveAccessQuery(
    Guid EmployeeProjectionId,
    SensitiveAccessDecisionRequest Request,
    IReadOnlyCollection<string> EffectivePermissions)
    : IRequest<Response<SensitiveAccessDecisionDto>>;
