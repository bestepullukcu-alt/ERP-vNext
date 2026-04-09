using MediatR;

namespace Diten.MdmService.Application.Features.Products;

public sealed record GetAllProductsQuery : IRequest<IReadOnlyList<ProductListItemDto>>;

public sealed record GetProductByIdQuery(Guid Id) : IRequest<ProductDetailDto?>;
