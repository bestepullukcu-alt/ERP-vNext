using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.OffboardingCases.Commands;

public sealed record UpdateOffboardingCaseCommand(Guid Id, OffboardingCaseUpdateRequest Request) : IRequest<Response<OffboardingCaseDto>>;
