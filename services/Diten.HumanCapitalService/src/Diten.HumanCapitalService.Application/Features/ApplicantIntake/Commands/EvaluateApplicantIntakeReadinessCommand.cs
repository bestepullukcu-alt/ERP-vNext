using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.ApplicantIntake.Commands;

public sealed record EvaluateApplicantIntakeReadinessCommand(Guid Id) : IRequest<Response<ApplicantIntakeReadinessDto>>;
