using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.EmployeeOnboarding.Commands;

public sealed record DeleteEmployeeOnboardingReadinessCommand(Guid Id) : IRequest<Response<bool>>;
