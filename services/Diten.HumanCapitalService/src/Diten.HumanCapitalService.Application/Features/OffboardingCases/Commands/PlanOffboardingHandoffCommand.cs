using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.OffboardingCases.Commands;

public sealed record PlanOffboardingHandoffCommand(Guid Id, OffboardingCaseHandoffRequest Request) : IRequest<Response<OffboardingCaseDto>>;
