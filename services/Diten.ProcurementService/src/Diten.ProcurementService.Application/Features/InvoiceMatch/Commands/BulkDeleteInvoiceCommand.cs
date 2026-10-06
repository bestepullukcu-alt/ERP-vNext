using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.InvoiceMatch.Commands;

/// <summary>Toplu soft delete (public InvoiceId listesi; pack §14 procurement.invoice-match.bulk-delete). Yalnız
/// Captured olanlar silinir; silinen adedini döner. Hard delete YOK.</summary>
public sealed record BulkDeleteInvoiceCommand(List<string> InvoiceIds) : IRequest<Response<int>>;
