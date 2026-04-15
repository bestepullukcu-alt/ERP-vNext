using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.Products.Queries.GetProductById;

public sealed record GetProductByIdQuery(Guid Id) : IRequest<Response<ProductDto>>;
