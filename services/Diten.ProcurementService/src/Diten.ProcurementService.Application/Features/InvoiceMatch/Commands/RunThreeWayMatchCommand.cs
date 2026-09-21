using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.InvoiceMatch.Commands;

/// <summary>
/// runThreeWayMatch (contract POST /api/invoice-match/invoices/{invoiceId}/match). PO (0141) ↔ GRN (0142) ↔ Invoice
/// eşleştirmesini POLICY-DRIVEN tolerans profiline göre (ASSUMPTION-P2P-01) yürütür; sonuç Matched |
/// MatchedWithinTolerance | Exception + variances. toleranceProfileId opsiyonel — verilmezse LE/tenant default
/// profil IMatchTolerancePolicy seam'inde çözülür (match mantığına sabit sayı GÖMÜLMEZ). Idempotency-Key ile
/// idempotent (replay → aynı outcome, ikinci exception YOK). Zaten Matched faturaya tekrar match → 409 INVALID_STATE.
/// </summary>
public sealed record RunThreeWayMatchCommand(
    string InvoiceId,
    string? ToleranceProfileId,
    string? IdempotencyKey) : IRequest<Response<MatchOutcomeDto>>;
