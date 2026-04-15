using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.Products.Commands.BulkDeleteProducts;

public sealed record BulkDeleteProductsCommand(List<Guid> Ids) : IRequest<Response<int>>;
