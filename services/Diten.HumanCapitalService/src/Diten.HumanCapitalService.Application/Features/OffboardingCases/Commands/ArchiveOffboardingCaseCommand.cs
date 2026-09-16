using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.OffboardingCases.Commands;

public sealed record ArchiveOffboardingCaseCommand(Guid Id) : IRequest<Response<bool>>;
