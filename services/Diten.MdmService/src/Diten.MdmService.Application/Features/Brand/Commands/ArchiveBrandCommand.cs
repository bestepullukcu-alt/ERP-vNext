using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.Brand.Commands;

// Soft archive only. There is deliberately no DeleteBrandCommand / BulkDeleteBrandCommand: FU01 §3 forbids
// hard delete, so the capability does not exist at any layer (command, handler, controller, gateway route).
public sealed record ArchiveBrandCommand(Guid BrandId, string? Actor = null) : IRequest<Response<NoContent>>;
