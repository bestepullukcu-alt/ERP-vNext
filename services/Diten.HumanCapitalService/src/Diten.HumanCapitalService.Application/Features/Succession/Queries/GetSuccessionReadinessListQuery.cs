using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.Succession.Queries;

public sealed record GetSuccessionReadinessListQuery : IRequest<Response<IReadOnlyList<SuccessionReadinessListItemDto>>>;
