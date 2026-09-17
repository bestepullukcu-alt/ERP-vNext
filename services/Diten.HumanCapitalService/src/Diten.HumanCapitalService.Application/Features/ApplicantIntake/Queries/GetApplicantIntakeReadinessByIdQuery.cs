using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.ApplicantIntake.Queries;

public sealed record GetApplicantIntakeReadinessByIdQuery(Guid Id) : IRequest<Response<ApplicantIntakeReadinessDto>>;
