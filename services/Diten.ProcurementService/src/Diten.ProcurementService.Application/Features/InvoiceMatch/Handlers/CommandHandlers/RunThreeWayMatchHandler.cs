using System.Globalization;
using Diten.ProcurementService.Application.Common;
using Diten.ProcurementService.Application.Features.InvoiceMatch.Commands;
using Diten.ProcurementService.Application.Features.InvoiceMatch.Validators;
using Diten.ProcurementService.Domain.Entities;
using Diten.ProcurementService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;
using GrnStatusEnum = Diten.ProcurementService.Domain.Entities.GrnStatus;
using InvoiceStatusEnum = Diten.ProcurementService.Domain.Entities.InvoiceStatus;

namespace Diten.ProcurementService.Application.Features.InvoiceMatch.Handlers.CommandHandlers;

/// <summary>
/// runThreeWayMatch — 3-yönlü eşleştirme (PO ↔ GRN ↔ Invoice). intra-service consume: PO (0141) IPurchaseOrderRepository,
/// alınan miktarlar GRN (0142) IGrnRepository üzerinden. Karşılaştırma alanları: quantity (fatura miktarı ↔ GRN'de
/// alınan miktar), price (fatura birim fiyat ↔ PO birim fiyat), amount (fatura satır tutarı ↔ PO fiyatıyla beklenen).
///
/// ⚠ TOLERANS POLICY-DRIVEN (ASSUMPTION-P2P-01): eşikler <see cref="IMatchTolerancePolicy"/> seam'inden
/// (toleranceProfileId → qty/price/amount) çözülür; bu handler'da HİÇBİR sabit sayısal tolerans YOKTUR. result =
/// Matched (sapma yok) | MatchedWithinTolerance (sapma policy içinde) | Exception (policy dışı → kuyruğa MatchException).
/// PO yok → NoPo; GRN yok → NoReceipt; currency uyumsuz → CurrencyMismatch. Idempotent (Idempotency-Key → aynı
/// outcome, ikinci exception YOK). Zaten sonuçlanmış faturaya tekrar match → 409 INVALID_STATE. Ödeme YÜRÜTMEZ.
/// </summary>
public sealed class RunThreeWayMatchHandler : IRequestHandler<RunThreeWayMatchCommand, Response<MatchOutcomeDto>>
{
    private readonly IInvoiceMatchRepository _repository;
    private readonly IPurchaseOrderRepository _purchaseOrderRepository;
    private readonly IGrnRepository _grnRepository;
    private readonly IMatchTolerancePolicy _tolerancePolicy;

    public RunThreeWayMatchHandler(
        IInvoiceMatchRepository repository,
        IPurchaseOrderRepository purchaseOrderRepository,
        IGrnRepository grnRepository,
        IMatchTolerancePolicy tolerancePolicy)
    {
        _repository = repository;
        _purchaseOrderRepository = purchaseOrderRepository;
        _grnRepository = grnRepository;
        _tolerancePolicy = tolerancePolicy;
    }

    public async Task<Response<MatchOutcomeDto>> Handle(RunThreeWayMatchCommand request, CancellationToken cancellationToken)
    {
        var invoice = await _repository.GetByInvoiceIdAsync(request.InvoiceId, cancellationToken);
        if (invoice is null)
        {
            return Response<MatchOutcomeDto>.Fail("NOT_FOUND", 404);
        }

        // ── Idempotent replay: aynı match key → persist edilmiş outcome döner (ikinci exception YOK) ──
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey)
            && string.Equals(invoice.MatchIdempotencyKey, request.IdempotencyKey, StringComparison.Ordinal)
            && invoice.LastMatchResult is not null)
        {
            return Response<MatchOutcomeDto>.Success(InvoiceMatchMapping.ToOutcomeDto(invoice));
        }

        // ── State guard: yalnız Captured/Exception match'e açık; zaten Matched/... faturaya tekrar match → 409 ──
        if (invoice.Status is InvoiceStatusEnum.Matched
            or InvoiceStatusEnum.MatchedWithinTolerance
            or InvoiceStatusEnum.Rejected
            or InvoiceStatusEnum.ClearedForPayment)
        {
            return Response<MatchOutcomeDto>.Fail("INVALID_STATE", 409);
        }

        // ── Tolerans POLICY-DRIVEN çözülür (seam; sabit sayı GÖMÜLMEZ) ──
        var tolerance = await _tolerancePolicy.ResolveAsync(request.ToleranceProfileId, cancellationToken);

        var po = await _purchaseOrderRepository.GetByPoIdAsync(invoice.PoId, cancellationToken);
        var grns = (await _grnRepository.GetAllAsync(invoice.PoId, null, cancellationToken))
            .Where(g => g.Status == GrnStatusEnum.Posted)
            .ToList();
        var grnIds = grns.Select(g => g.GrnId).ToList();

        var variances = new List<MatchVariance>();
        MatchExceptionReason? reason = null;

        if (po is null)
        {
            // Consumed PO artık çözülemiyor → NoPo (fail-closed; kimlik uydurulmaz).
            reason = MatchExceptionReason.NoPo;
        }
        else if (!string.Equals(invoice.Currency.Trim(), po.Currency?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            reason = MatchExceptionReason.CurrencyMismatch;
        }
        else if (grns.Count == 0)
        {
            reason = MatchExceptionReason.NoReceipt;
        }
        else
        {
            reason = EvaluateLineVariances(invoice, po, grns, tolerance, variances);
        }

        // ── Sonuç kararı ──
        InvoiceMatchResult result;
        if (reason is not null)
        {
            result = InvoiceMatchResult.Exception;
        }
        else if (variances.Count == 0)
        {
            result = InvoiceMatchResult.Matched;
        }
        else
        {
            // Sapmalar var ama hepsi policy toleransı içinde (aksi reason set edilirdi).
            result = InvoiceMatchResult.MatchedWithinTolerance;
        }

        var matchKey = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey;

        // ── Exception ise kuyruğa MatchException düşür ──
        string? exceptionId = null;
        if (result == InvoiceMatchResult.Exception)
        {
            var exception = new MatchException
            {
                ExceptionId = GenerateExceptionId(),
                InvoiceId = invoice.InvoiceId,
                PoId = po?.PoId ?? invoice.PoId,
                ReasonCode = reason!.Value,
                Status = MatchExceptionStatus.Open
            };
            var createdException = await _repository.CreateExceptionAsync(exception, cancellationToken);
            exceptionId = createdException.ExceptionId;
        }

        // ── Invoice'a match outcome persist (optimistic concurrency) ──
        invoice.LastMatchResult = result;
        invoice.Variances = variances;
        invoice.GrnIds = grnIds;
        invoice.ToleranceProfileId = tolerance.ProfileId;
        invoice.ExceptionId = exceptionId;
        invoice.MatchIdempotencyKey = matchKey;
        invoice.Status = result switch
        {
            InvoiceMatchResult.Matched => InvoiceStatusEnum.Matched,
            InvoiceMatchResult.MatchedWithinTolerance => InvoiceStatusEnum.MatchedWithinTolerance,
            _ => InvoiceStatusEnum.Exception
        };

        var ok = await _repository.UpdateAsync(invoice, invoice.Version, cancellationToken);
        if (!ok)
        {
            return Response<MatchOutcomeDto>.Fail("INVALID_STATE", 409);
        }

        return Response<MatchOutcomeDto>.Success(InvoiceMatchMapping.ToOutcomeDto(invoice));
    }

    /// <summary>
    /// Satır bazlı sapma değerlendirmesi (quantity/price/amount). Sapma bulunan her alan için bir
    /// <see cref="MatchVariance"/> üretilir; <c>withinTolerance</c> policy eşiğine (seam) göre işaretlenir. Policy
    /// DIŞINDA bir sapma varsa ilk ihlal alanının sebep kodunu döner (aksi null → tüm sapmalar tolerans içinde).
    /// </summary>
    private static MatchExceptionReason? EvaluateLineVariances(
        Domain.Entities.Invoice invoice,
        Domain.Entities.PurchaseOrder po,
        IReadOnlyList<GoodsReceipt> grns,
        MatchToleranceProfile tolerance,
        List<MatchVariance> variances)
    {
        MatchExceptionReason? reason = null;

        foreach (var invLine in invoice.Lines)
        {
            var poLine = FindPoLine(po, invLine);
            var receivedQty = ReceivedQuantity(grns, invLine);

            InvoiceMatchValidationRules.TryParseDecimal(invLine.Quantity, out var invQty);
            InvoiceMatchValidationRules.TryParseDecimal(invLine.UnitPrice, out var invPrice);
            InvoiceMatchValidationRules.TryParseDecimal(invLine.LineAmount, out var invAmount);

            var poPrice = 0m;
            if (poLine is not null)
            {
                InvoiceMatchValidationRules.TryParseDecimal(poLine.UnitPrice, out poPrice);
            }

            // quantity: fatura miktarı ↔ GRN'de alınan miktar.
            var qtyDelta = Math.Abs(invQty - receivedQty);
            if (qtyDelta != 0m)
            {
                var within = qtyDelta <= tolerance.QuantityTolerance;
                variances.Add(new MatchVariance
                {
                    PoLineId = invLine.PoLineId,
                    Field = VarianceFields.Quantity,
                    Expected = Fmt(receivedQty),
                    Actual = Fmt(invQty),
                    WithinTolerance = within
                });
                if (!within)
                {
                    reason ??= MatchExceptionReason.QtyMismatch;
                }
            }

            // price: fatura birim fiyat ↔ PO birim fiyat.
            var priceDelta = Math.Abs(invPrice - poPrice);
            if (priceDelta != 0m)
            {
                var within = priceDelta <= tolerance.PriceTolerance;
                variances.Add(new MatchVariance
                {
                    PoLineId = invLine.PoLineId,
                    Field = VarianceFields.Price,
                    Expected = Fmt(poPrice),
                    Actual = Fmt(invPrice),
                    WithinTolerance = within
                });
                if (!within)
                {
                    reason ??= MatchExceptionReason.PriceMismatch;
                }
            }

            // amount: fatura satır tutarı ↔ PO fiyatıyla beklenen tutar (fatura miktarı × PO birim fiyat).
            var expectedAmount = invQty * poPrice;
            var amountDelta = Math.Abs(invAmount - expectedAmount);
            if (amountDelta != 0m)
            {
                var within = amountDelta <= tolerance.AmountTolerance;
                variances.Add(new MatchVariance
                {
                    PoLineId = invLine.PoLineId,
                    Field = VarianceFields.Amount,
                    Expected = Fmt(expectedAmount),
                    Actual = Fmt(invAmount),
                    WithinTolerance = within
                });
                if (!within)
                {
                    reason ??= MatchExceptionReason.AmountMismatch;
                }
            }
        }

        return reason;
    }

    private static PoLine? FindPoLine(Domain.Entities.PurchaseOrder po, InvoiceLine invLine)
    {
        if (!string.IsNullOrWhiteSpace(invLine.PoLineId))
        {
            var byId = po.Lines.FirstOrDefault(p => string.Equals(p.PoLineId, invLine.PoLineId, StringComparison.Ordinal));
            if (byId is not null)
            {
                return byId;
            }
        }
        return po.Lines.FirstOrDefault(p => string.Equals(p.ItemId, invLine.ItemId, StringComparison.Ordinal));
    }

    /// <summary>GRN'lerde bu fatura satırı için alınan toplam miktar (poLineId eşleşmesi, yoksa itemId).</summary>
    private static decimal ReceivedQuantity(IReadOnlyList<GoodsReceipt> grns, InvoiceLine invLine)
    {
        var total = 0m;
        foreach (var grn in grns)
        {
            foreach (var gl in grn.Lines)
            {
                var matches = !string.IsNullOrWhiteSpace(invLine.PoLineId)
                    ? string.Equals(gl.PoLineId, invLine.PoLineId, StringComparison.Ordinal)
                    : string.Equals(gl.ItemId, invLine.ItemId, StringComparison.Ordinal);
                if (matches)
                {
                    InvoiceMatchValidationRules.TryParseDecimal(gl.Quantity, out var q);
                    total += q;
                }
            }
        }
        return total;
    }

    private static string Fmt(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    private static string GenerateExceptionId()
        => "EXC-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
}
