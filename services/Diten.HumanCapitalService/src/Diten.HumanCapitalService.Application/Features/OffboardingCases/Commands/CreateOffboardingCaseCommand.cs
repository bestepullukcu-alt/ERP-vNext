using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.OffboardingCases.Commands;

public sealed record CreateOffboardingCaseCommand(OffboardingCaseCreateRequest Request) : IRequest<Response<Guid>>;
