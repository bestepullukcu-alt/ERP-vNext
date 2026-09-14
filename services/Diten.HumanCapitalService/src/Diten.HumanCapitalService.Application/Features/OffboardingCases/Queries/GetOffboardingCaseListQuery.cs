using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.OffboardingCases.Queries;

public sealed record GetOffboardingCaseListQuery : IRequest<Response<IReadOnlyList<OffboardingCaseListItemDto>>>;
