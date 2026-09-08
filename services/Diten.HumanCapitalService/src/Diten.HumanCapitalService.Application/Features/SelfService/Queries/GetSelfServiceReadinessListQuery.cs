using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.SelfService.Queries;

public sealed record GetSelfServiceReadinessListQuery : IRequest<Response<IReadOnlyList<SelfServiceReadinessListItemDto>>>;
