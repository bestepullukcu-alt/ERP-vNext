using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Supplier.Commands;

/// <summary>Toplu soft delete (public SupplierId listesi). Silinen adedini döner.</summary>
public sealed record BulkDeleteSupplierCommand(List<string> SupplierIds) : IRequest<Response<int>>;
