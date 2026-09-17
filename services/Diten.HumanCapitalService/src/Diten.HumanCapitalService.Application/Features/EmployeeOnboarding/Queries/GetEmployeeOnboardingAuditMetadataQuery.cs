using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.EmployeeOnboarding.Queries;

public sealed record GetEmployeeOnboardingAuditMetadataQuery(Guid Id) : IRequest<Response<EmployeeOnboardingAuditMetadataDto>>;
