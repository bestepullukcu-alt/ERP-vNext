using MediatR;

namespace Diten.MdmService.Application.Features.Skus;

public sealed record GetSkusQuery() : IRequest<IReadOnlyList<SkuListItemDto>>;

public sealed record GetSkuByIdQuery(Guid Id) : IRequest<SkuDetailDto?>;
