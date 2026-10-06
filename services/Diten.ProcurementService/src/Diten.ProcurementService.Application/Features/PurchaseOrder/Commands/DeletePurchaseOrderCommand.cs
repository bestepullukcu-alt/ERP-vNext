using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.PurchaseOrder.Commands;

/// <summary>Soft delete tek PO (public PoId). Yalnız Draft silinebilir (Draft dışı → 409). Hard delete YOK.</summary>
public sealed record DeletePurchaseOrderCommand(string PoId) : IRequest<Response<bool>>;
