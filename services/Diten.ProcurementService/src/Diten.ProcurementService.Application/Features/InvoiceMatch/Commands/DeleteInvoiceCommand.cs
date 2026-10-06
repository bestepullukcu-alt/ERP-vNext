using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.InvoiceMatch.Commands;

/// <summary>Soft delete tek Invoice (public InvoiceId; pack §14 procurement.invoice-match.delete). Yalnız Captured
/// silinebilir; eşleşmiş/çözülmüş fatura silinemez (→ 409 INVALID_STATE). Hard delete YOK.</summary>
public sealed record DeleteInvoiceCommand(string InvoiceId) : IRequest<Response<bool>>;
