using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.Product.Commands;

// Soft archive only — no DeleteProductCommand / BulkDeleteProductCommand exists anywhere in this feature.
// Archiving a product never removes Campaign / Knowledge / Frequency references to it (FU01 §11).
public sealed record ArchiveProductCommand(Guid ProductId, string? Actor = null) : IRequest<Response<NoContent>>;
