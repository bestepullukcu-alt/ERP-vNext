using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.InvoiceMatch.Commands;

/// <summary>
/// resolveMatchException (contract POST /api/invoice-match/exceptions/{exceptionId}/resolve). decision =
/// approve | reject | tolerance-override; approval trail (MOD-0023 seam) + audit ile exception'ı Resolved yapar ve
/// invoice durumunu günceller: approve → ClearedForPayment (ÖDEME YÜRÜTMEZ — yalnız işaret; AP/payment=Finance),
/// tolerance-override → MatchedWithinTolerance, reject → Rejected. Idempotency-Key ile idempotent. Kapalı exception
/// resolve → 409 INVALID_STATE. Cross-tenant/LE → 404.
/// </summary>
public sealed record ResolveMatchExceptionCommand(
    string ExceptionId,
    string Decision,
    string? Note,
    string? IdempotencyKey) : IRequest<Response<MatchOutcomeDto>>;
