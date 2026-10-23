using Diten.CrmService.Application.Features.Commercial.Pricing.Contracts;
using Diten.CrmService.Domain.Entities.Commercial;

namespace Diten.CrmService.Application.Features.Commercial.PreOrders;

// WP-VW-W6 W6-BE-a — MOD-0168: every input / DTO in ONE file (§10). Amounts, prices and percentages are MOD-0156's
// numbers as they were evaluated; nothing here computes them.

public sealed record PreOrderMfInput(int Buy, int Free);

public sealed record PreOrderLineInput(Guid? SkuId, int? Quantity, PreOrderMfInput? Mf = null, decimal? DiscountPct = null);

/// <summary>A draft. Seller, country, currencies and price mode are NEVER in the input (K-O4).</summary>
public sealed record PreOrderInput(
    Guid? PharmacyAccountId, Guid? DepotAccountId, DateOnly? RequestedDeliveryDate, string? Note, Guid? VisitId,
    IReadOnlyList<PreOrderLineInput>? Lines, int? ExpectedVersion = null);

public sealed record PreOrderPreviewInput(Guid? PharmacyAccountId, IReadOnlyList<PreOrderLineInput>? Lines);

public sealed record PreOrderSignatureInput(string? SignerName, string? ImageBase64, string? MediaType, int? ExpectedVersion = null);

public sealed record PreOrderVersionInput(int? ExpectedVersion = null);

/// <summary><c>approve</c> | <c>reject</c> | <c>request-changes</c>; the last two need a reason. <see cref="LinkToken"/> =
/// the decision comes from the e-mail link (the signed-in approver; the link is consumed once).</summary>
public sealed record PreOrderDecisionInput(string? Decision, string? Reason, string? LinkToken = null, int? ExpectedVersion = null);

public sealed record PreOrderCancelInput(string? Reason, int? ExpectedVersion = null);

public sealed record PreOrderPartialLineInput(int LineNo, int DeliveredQuantity);

public sealed record PreOrderDepotResponseInput(string? Status, IReadOnlyList<PreOrderPartialLineInput>? PartialLines, string? Note,
    int? ExpectedVersion = null);

public sealed record PreOrderCountryTierInput(string? CountryCode, string? Tier);

public sealed record PreOrderCompanyPrefixInput(Guid? LegalEntityId, string? Prefix);

public sealed record PreOrderSettingsInput(
    string? ApprovalMode, int? ApprovalLinkTtlHours, IReadOnlyList<PreOrderCountryTierInput>? PriceTierByCountry, int? TransmissionRetryMax,
    IReadOnlyList<PreOrderCompanyPrefixInput>? CompanyPrefixes, int? ExpectedVersion = null);

// ── DTOs ─────────────────────────────────────────────────────────────────────────────────────────────────────────

/// <summary>What the caller may do now (the screen enables its buttons from it; the server checks again).</summary>
public sealed record PreOrderAbilities(bool Edit, bool Submit, bool Decide, bool Cancel, bool Copy, bool RecordDepotResponse, bool RetryTransmission);

public sealed record PreOrderDto(
    Guid Id, string Code, string Status, string Fulfilment, string PriceMode, PreOrderSeller Seller, string CountryCode, string LocalCurrencyCode,
    string? Currency, PreOrderParty Pharmacy, PreOrderParty? Depot, PreOrderRep Rep, Guid? VisitId, DateOnly? RequestedDeliveryDate,
    string? Note, IReadOnlyList<PreOrderLine> Lines, PreOrderTotals? Totals, PreOrderFxSnapshot? Fx, PreOrderPriceSnapshot? Price,
    PreOrderEvaluationSnapshot? Evaluation, PreOrderApprovalRequirement? ApprovalRequirement, PreOrderSignature? Signature,
    PreOrderApproval? Approval, PreOrderTransmission? Transmission, PreOrderCancellation? Cancellation, PreOrderDepotResponse? DepotResponse,
    Guid? CopiedFromPreOrderId, int SubmissionNo, IReadOnlyList<PreOrderEvent> Events, string? CreatedBy, DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt, int Version, PreOrderAbilities Can);

public sealed record PreOrderRowDto(
    Guid Id, string Code, DateTimeOffset CreatedAt, string Status, Guid PharmacyAccountId, string PharmacyName, string? PharmacyCityRef,
    Guid? DepotAccountId, string? DepotName, Guid RepUserId, string? RepName, int LineCount, string PriceMode, string? Currency, decimal? Gross,
    string LocalCurrencyCode, decimal? LocalGross, decimal? EffectiveDiscountPct, string? Outcome, string? DepotResponseStatus, bool AwaitingMe,
    string SellerCode, string CountryCode);

public sealed record PreOrderPageDto(IReadOnlyList<PreOrderRowDto> Items, long Total, int Page, int PageSize);

public sealed record PreOrderApproverPreview(bool Available, string Resolution, string? PositionCode, string? PositionName,
    IReadOnlyList<PreOrderLineManager> Managers);

/// <summary>Screen A's "from the system 🔒" strip (K-O4) + the approver preview.</summary>
public sealed record PreOrderContextDto(
    Guid LegalEntityId, string LegalEntityCode, string LegalEntityName, string? CompanyPrefix, string CountryCode, string LocalCurrencyCode,
    string PriceMode, string? Currency, PriceListRef? PriceList, string Tier, string ApprovalMode, PreOrderApproverPreview Approver);

public sealed record PreOrderFindingDto(string Code, string Severity, IReadOnlyDictionary<string, string?> Params);

public sealed record PreOrderPreviewLineDto(
    int LineNo, Guid SkuId, string Outcome, decimal? UnitPrice, decimal? VatRate, decimal? NetAmount, decimal? VatAmount, int FreeQuantity,
    decimal? MfRatioPct, decimal? CombinedDiscountPct, IReadOnlyList<PreOrderFindingDto> Findings, Guid? RuleKey, int? RuleVersion);

public sealed record PreOrderPreviewDto(
    string Mode, string? Currency, PriceListRef? PriceList, string Tier, string Outcome, IReadOnlyList<PreOrderPreviewLineDto> Lines,
    EvaluationTotals? Totals, PreOrderFxSnapshot? Fx, bool ApprovalRequired, IReadOnlyList<string> ApprovalReasons, string RuleSetFingerprint);

public sealed record PreOrderSettingsDto(
    string ApprovalMode, int ApprovalLinkTtlHours, IReadOnlyList<PreOrderCountryTier> PriceTierByCountry, int TransmissionRetryMax,
    IReadOnlyList<PreOrderCompanyPrefix> CompanyPrefixes, int Version, string? UpdatedBy, DateTimeOffset? UpdatedAt, bool IsDefault);

public sealed record PreOrderApprovalLinkDto(string State, DateTimeOffset ExpiresAt, Guid PreOrderId, PreOrderDto? PreOrder);

public sealed record PreOrderHistoryItemDto(Guid Id, string Code, DateTimeOffset CreatedAt, string Status, int LineCount, int Quantity,
    string PriceMode, string? Currency, decimal? Gross,
    /// <summary>W6-FIX-d (V-9) — the order's effective discount (MF + discount, MOD-0156); null for a quantity-only order.</summary>
    decimal? EffectiveDiscountPct = null);

/// <summary>One currency's amount — a month's gross is never summed across currencies (AC-UI-2).</summary>
public sealed record PreOrderMoneyDto(string Currency, decimal Amount);

/// <summary>W6-FIX-d (V-9) — one month (<c>yyyy-MM</c>, UTC) of the pharmacy's submitted orders: the count, the boxes, the gross
/// PER CURRENCY and the effective discount (net-weighted, priced orders only; null when the month has none).</summary>
public sealed record PreOrderHistoryMonthDto(string Month, int Orders, int Boxes, IReadOnlyList<PreOrderMoneyDto> Gross, decimal? EffectiveDiscountPct);

public sealed record PreOrderPharmacyHistoryDto(Guid PharmacyAccountId, DateTimeOffset From, IReadOnlyList<PreOrderHistoryItemDto> Items,
    IReadOnlyList<PreOrderHistoryMonthDto>? Months = null);

/// <summary>
/// WP-VW-W6 W6-FIX-d (V-9, mockup C) — the approver's six months of the pharmacy, computed HERE (the screen draws, it does not
/// calculate). One bucket per calendar month (UTC), oldest first, the current month last, an empty month included. A
/// cancelled / rejected order is not in a month (it was never fulfilled). A month's effective discount is the net-weighted
/// average of its priced orders' own effective discount; a quantity-only order (no prices) has no effective discount and
/// stays out of it. The weight is the order's net in its LOCAL currency (the net itself when the list currency is the local
/// one, else net × the order's own FX snapshot rate), so orders in different currencies weigh by the same unit; a foreign-
/// currency order without an FX snapshot cannot be weighed and stays out of the percentage (it still counts as an order).
/// </summary>
public static class PreOrderPharmacyHistory
{
    public const int Months = 6;

    public static PreOrderHistoryItemDto Item(PreOrder o)
        => new(o.Id, o.Code, o.CreatedAt, o.Status, o.Lines.Count, o.Lines.Sum(l => l.Quantity), o.PriceMode, o.Currency, o.Totals?.Gross,
            IsPriced(o) ? o.Totals!.EffectiveDiscountPct : null);

    public static IReadOnlyList<PreOrderHistoryMonthDto> MonthsOf(IEnumerable<PreOrder> submitted, DateTimeOffset now)
    {
        var current = new DateTime(now.UtcDateTime.Year, now.UtcDateTime.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var counted = submitted.Where(o => o.Status is not (PreOrderStatuses.Cancelled or PreOrderStatuses.Rejected)).ToList();
        return Enumerable.Range(0, Months).Select(i => current.AddMonths(i - (Months - 1))).Select(start =>
        {
            var month = counted.Where(o => o.CreatedAt.UtcDateTime.Year == start.Year && o.CreatedAt.UtcDateTime.Month == start.Month).ToList();
            var gross = month.Where(o => IsPriced(o) && !string.IsNullOrWhiteSpace(o.Currency))
                .GroupBy(o => o.Currency!.ToUpperInvariant()).OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => new PreOrderMoneyDto(g.Key, g.Sum(o => o.Totals!.Gross))).ToList();
            return new PreOrderHistoryMonthDto(start.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture), month.Count,
                month.Sum(o => o.Lines.Sum(l => l.Quantity)), gross, EffectiveDiscount(month));
        }).ToList();
    }

    /// <summary>Σ(effective % × local net) / Σ local net over the month's priced orders that can be weighed; null without any.</summary>
    public static decimal? EffectiveDiscount(IEnumerable<PreOrder> month)
    {
        decimal weighted = 0m, weight = 0m;
        foreach (var o in month.Where(IsPriced))
        {
            if (LocalNet(o) is not { } net || net <= 0m)
            {
                continue;
            }

            weighted += o.Totals!.EffectiveDiscountPct * net;
            weight += net;
        }

        return weight > 0m ? weighted / weight : null;
    }

    private static bool IsPriced(PreOrder o) => o.PriceMode == PreOrderPriceModes.Priced && o.Totals is not null;

    private static decimal? LocalNet(PreOrder o)
        => string.Equals(o.Currency, o.LocalCurrencyCode, StringComparison.OrdinalIgnoreCase) ? o.Totals!.Net
            : o.Fx is { Rate: > 0m } fx ? o.Totals!.Net * fx.Rate
            : null;
}

public sealed record PreOrderExportDto(string FileName, byte[] Content);

/// <summary>
/// WP-VW-W6 W6-FIX-c (V-3 / V-15) — the totals as they are read: an order saved before W6-FIX-c has no list subtotal and no
/// discount amount, so they are completed from its stored lines by MOD-0156 (<see cref="PricingTotals"/>, the ONE place of
/// that arithmetic) — no migration, nothing written. The stored entity is never changed (a copy is returned).
/// </summary>
public static class PreOrderTotalsReading
{
    public static PreOrderTotals? Of(PreOrder o)
    {
        if (o.Totals is not { } t)
        {
            return null;
        }

        var completed = t.ListSubtotal is { } stored && t.DiscountAmount is { } discount
            ? (ListSubtotal: stored, DiscountAmount: discount)
            : PricingTotals.ListAndDiscount(o.Lines.Select(l => (l.Quantity, l.UnitPrice)), t.Net, o.Currency);
        return new PreOrderTotals
        {
            Net = t.Net, Vat = t.Vat, Gross = t.Gross, EffectiveDiscountPct = t.EffectiveDiscountPct, ListSubtotal = completed.ListSubtotal,
            DiscountAmount = completed.DiscountAmount
        };
    }
}

public static class PreOrderMapping
{
    public static PreOrderDto ToDto(PreOrder o, PreOrderAbilities can)
        => new(o.Id, o.Code, o.Status, o.Fulfilment, o.PriceMode, o.Seller, o.CountryCode, o.LocalCurrencyCode, o.Currency, o.Pharmacy, o.Depot,
            o.Rep, o.VisitId, o.RequestedDeliveryDate, o.Note, o.Lines, PreOrderTotalsReading.Of(o), o.Fx, o.Price, o.Evaluation, o.ApprovalRequirement, o.Signature,
            o.Approval, o.Transmission, o.Cancellation, o.DepotResponse, o.CopiedFromPreOrderId, o.SubmissionNo, o.Events, o.CreatedBy,
            o.CreatedAt, o.UpdatedAt, o.Version, can);

    public static PreOrderRowDto ToRow(PreOrder o, Guid? caller)
        => new(o.Id, o.Code, o.CreatedAt, o.Status, o.Pharmacy.AccountId, o.Pharmacy.Name, o.Pharmacy.CityRef, o.Depot?.AccountId, o.Depot?.Name,
            o.Rep.UserId, o.Rep.DisplayName, o.Lines.Count, o.PriceMode, o.Currency, o.Totals?.Gross, o.LocalCurrencyCode, o.Fx?.LocalGross,
            o.Totals?.EffectiveDiscountPct, o.Evaluation?.Outcome, o.DepotResponse?.Status,
            o.Status == PreOrderStatuses.PendingApproval && caller is { } me && o.ApprovalRequirement?.ApproverUserIds.Contains(me) == true,
            o.Seller.Prefix, o.CountryCode);

    public static PreOrderSettingsDto ToDto(PreOrderSettings s, bool isDefault)
        => new(s.ApprovalMode, s.ApprovalLinkTtlHours, s.PriceTierByCountry, s.TransmissionRetryMax, s.CompanyPrefixes, s.Version, s.UpdatedBy,
            s.UpdatedAt, isDefault);

    public static PreOrderFindingDto ToDto(PreOrderFinding f) => new(f.Code, f.Severity, f.Params);
}
