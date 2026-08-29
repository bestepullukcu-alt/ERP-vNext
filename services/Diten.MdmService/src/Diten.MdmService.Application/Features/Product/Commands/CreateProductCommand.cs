using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.Product.Commands;

public sealed record CreateProductCommand(ProductWriteRequest Request, string? Actor = null) : IRequest<Response<Guid>>;
