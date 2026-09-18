using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.PurchaseOrder.Commands;

/// <summary>Toplu soft delete (public PoId listesi). Yalnız Draft olanlar silinir; silinen adedini döner.</summary>
public sealed record BulkDeletePurchaseOrderCommand(List<string> PoIds) : IRequest<Response<int>>;
