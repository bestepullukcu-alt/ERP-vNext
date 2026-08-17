using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.HrisSources.Queries;

public sealed record GetHrisSourceProfileListQuery : IRequest<Response<IReadOnlyList<HrisSourceProfileListItemDto>>>;
