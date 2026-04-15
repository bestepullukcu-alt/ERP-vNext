using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.Products.Queries.GetProducts;

public sealed record GetProductsQuery : IRequest<Response<IReadOnlyList<ProductListDto>>>;
