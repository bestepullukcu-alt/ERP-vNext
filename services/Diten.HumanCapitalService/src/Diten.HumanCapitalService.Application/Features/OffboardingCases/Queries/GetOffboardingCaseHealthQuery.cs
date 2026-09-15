using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.OffboardingCases.Queries;

public sealed record GetOffboardingCaseHealthQuery : IRequest<Response<OffboardingCaseHealthDto>>;
