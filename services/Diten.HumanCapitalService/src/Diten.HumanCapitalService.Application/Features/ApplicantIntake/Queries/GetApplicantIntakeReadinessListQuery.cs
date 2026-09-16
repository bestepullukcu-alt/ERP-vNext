using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.ApplicantIntake.Queries;

public sealed record GetApplicantIntakeReadinessListQuery : IRequest<Response<IReadOnlyList<ApplicantIntakeReadinessListItemDto>>>;
