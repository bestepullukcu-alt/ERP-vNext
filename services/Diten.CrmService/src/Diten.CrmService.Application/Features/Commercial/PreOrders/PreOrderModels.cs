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
    string PriceMode, string? Currency, decimal? Gross);

public sealed record PreOrderPharmacyHistoryDto(Guid PharmacyAccountId, DateTimeOffset From, IReadOnlyList<PreOrderHistoryItemDto> Items);

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
