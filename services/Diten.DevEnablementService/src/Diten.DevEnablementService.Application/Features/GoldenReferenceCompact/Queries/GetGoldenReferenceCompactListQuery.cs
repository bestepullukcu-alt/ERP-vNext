using Diten.Shared.Core;
using MediatR;

namespace Diten.DevEnablementService.Application.Features.GoldenReferenceCompact.Queries;

public sealed record GetGoldenReferenceCompactListQuery() : IRequest<Response<IReadOnlyList<GoldenReferenceCompactListItemDto>>>;
