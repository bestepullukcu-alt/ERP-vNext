using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.EmployeeOnboarding.Queries;

public sealed record GetEmployeeOnboardingReadinessListQuery : IRequest<Response<IReadOnlyList<EmployeeOnboardingReadinessListItemDto>>>;
