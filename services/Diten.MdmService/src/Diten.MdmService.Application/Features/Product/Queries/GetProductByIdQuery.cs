using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.Product.Queries;

public sealed record GetProductByIdQuery(Guid ProductId) : IRequest<Response<ProductDetailDto>>;
