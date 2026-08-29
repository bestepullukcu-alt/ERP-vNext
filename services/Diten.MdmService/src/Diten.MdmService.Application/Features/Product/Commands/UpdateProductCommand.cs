using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.Product.Commands;

public sealed record UpdateProductCommand(Guid ProductId, ProductWriteRequest Request, string? Actor = null)
    : IRequest<Response<NoContent>>;
