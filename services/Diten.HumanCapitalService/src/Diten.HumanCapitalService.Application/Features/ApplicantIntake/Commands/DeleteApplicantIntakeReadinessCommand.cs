using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.ApplicantIntake.Commands;

public sealed record DeleteApplicantIntakeReadinessCommand(Guid Id) : IRequest<Response<bool>>;
