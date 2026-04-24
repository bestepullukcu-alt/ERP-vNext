using Diten.Shared.Core;
using MediatR;

namespace Diten.DevEnablementService.Application.Features.GoldenReferenceItems.Queries;

public sealed record GetGoldenReferenceItemListQuery() : IRequest<Response<IReadOnlyList<GoldenReferenceItemListItemDto>>>;
