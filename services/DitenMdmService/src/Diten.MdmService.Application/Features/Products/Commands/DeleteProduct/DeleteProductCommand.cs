using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.Products.Commands.DeleteProduct;

public sealed record DeleteProductCommand(Guid Id) : IRequest<Response<bool>>;
