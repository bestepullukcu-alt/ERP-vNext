using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.EmployeeOnboarding.Commands;

public sealed record EvaluateEmployeeOnboardingReadinessCommand(Guid Id) : IRequest<Response<EmployeeOnboardingReadinessDto>>;
