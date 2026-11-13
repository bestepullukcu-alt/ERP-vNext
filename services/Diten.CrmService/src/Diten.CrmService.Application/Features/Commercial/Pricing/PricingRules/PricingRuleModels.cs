using Diten.CrmService.Application.Features.Approvals;
using Diten.CrmService.Application.Features.Commercial.Pricing.Evaluation;
using Diten.CrmService.Domain.Entities.Commercial;

namespace Diten.CrmService.Application.Features.Commercial.Pricing.PricingRules;

// WP-VW-W6 W6-BE-b — MOD-0156 §4.3 pricing rules on the wire.

public sealed record PricingPctLimitInput(decimal? Soft, decimal? Hard);

/// <summary>"10+2" as buy 10 / free 2.</summary>
public sealed record PricingMfTermInput(int Buy, int Free);

public sealed record PricingMfLimitInput(PricingMfTermInput? Soft, PricingMfTermInput? Hard);

public sealed record PricingQuantityLimitInput(int? Min, int? SoftMax, int? HardMax);

/// <summary>A rule draft. The legal entity and the SKU are set when the rule is created and stay with its key.</summary>
public sealed record PricingRuleInput(
    Guid? LegalEntityId, Guid? SkuId, string? SegmentCode, DateOnly? ValidFrom, DateOnly? ValidTo,
    PricingPctLimitInput? MaxDiscountPct, PricingMfLimitInput? MaxMf, PricingPctLimitInput? MaxCombinedPct, PricingQuantityLimitInput? Quantity,
    int? ExpectedVersion = null);

/// <summary><c>POST …/{id}/revisions</c>: <c>revise</c> (a new version with the given limits) | <c>retire</c>.</summary>
public sealed record PricingRuleRevisionInput(string? ChangeKind, PricingRuleInput? Rule);

public sealed record PricingRuleDecisionInput(string? Decision, string? Reason, int? ExpectedVersion = null);

public sealed record PricingMfTermDto(int Buy, int Free, string Text, decimal RatioPct);

public sealed record PricingPctLimitDto(decimal? Soft, decimal? Hard);

public sealed record PricingMfLimitDto(PricingMfTermDto? Soft, PricingMfTermDto? Hard);

public sealed record PricingQuantityLimitDto(int? Min, int? SoftMax, int? HardMax);

public sealed record PricingRuleDto(
    Guid Id, Guid RuleKey, int RuleVersion, Guid LegalEntityId, string? LegalEntityName, Guid SkuId, string? SkuLevel, string? SkuCode,
    string? SkuName, string? SegmentCode, DateOnly? ValidFrom, DateOnly? ValidTo, PricingPctLimitDto MaxDiscountPct, PricingMfLimitDto MaxMf,
    PricingPctLimitDto MaxCombinedPct, PricingQuantityLimitDto Quantity, string Status, string ChangeKind, int Specificity,
    string? CreatedBy, DateTimeOffset CreatedAt, string? UpdatedBy, DateTimeOffset? UpdatedAt, string? ActivatedBy, DateTimeOffset? ActivatedAt,
    int Version, IReadOnlyList<ApprovalRoundDto> Rounds, IReadOnlyList<ApprovalDecisionDto> Decisions);

public sealed record PricingRulePageDto(IReadOnlyList<PricingRuleDto> Items, long Total, int Page, int PageSize);

public sealed record PricingRuleDetailDto(PricingRuleDto Rule, IReadOnlyList<PricingRuleDto> Versions);

/// <summary>Kural deneme (§1.1 #5): the product + quantity + MF + discount (+ company, segment, day) → the evaluation, saved nowhere.</summary>
public sealed record PricingTrialLineInput(Guid? SkuId, int? Quantity, PricingMfTermInput? Mf, decimal? DiscountPct);

public sealed record PricingTrialInput(
    Guid? LegalEntityId, string? CountryCode, DateOnly? AsOf, string? Tier, string? PharmacySegmentCode, IReadOnlyList<PricingTrialLineInput>? Lines);

public static class PricingRuleMapping
{
    public static PricingRuleDto ToDto(PricingRule r)
        => new(r.Id, r.RuleKey, r.RuleVersion, r.LegalEntityId, r.LegalEntityName, r.SkuId, r.SkuLevel, r.SkuCode, r.SkuName, r.SegmentCode,
            r.ValidFrom, r.ValidTo, new PricingPctLimitDto(r.MaxDiscountPct.Soft, r.MaxDiscountPct.Hard),
            new PricingMfLimitDto(Term(r.MaxMf.Soft), Term(r.MaxMf.Hard)), new PricingPctLimitDto(r.MaxCombinedPct.Soft, r.MaxCombinedPct.Hard),
            new PricingQuantityLimitDto(r.Quantity.Min, r.Quantity.SoftMax, r.Quantity.HardMax), r.Status, r.ChangeKind, RuleSpecificity.Of(r),
            r.CreatedBy, r.CreatedAt, r.UpdatedBy, r.UpdatedAt, r.ActivatedBy, r.ActivatedAt, r.Version, ApprovalRoundDto.From(r),
            ApprovalDecisionDto.From(r));

    private static PricingMfTermDto? Term(PricingMfTerm? term)
        => term is null ? null : new PricingMfTermDto(term.Buy, term.Free, PricingMath.MfText(term.Buy, term.Free), PricingMath.Pct(PricingMath.MfRatio(term)!.Value));
}
