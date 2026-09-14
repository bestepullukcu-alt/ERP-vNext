using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.EmployeeOnboarding.Queries;

public sealed record GetEmployeeOnboardingReadinessByIdQuery(Guid Id) : IRequest<Response<EmployeeOnboardingReadinessDto>>;
