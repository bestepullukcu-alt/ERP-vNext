using Diten.BuildingBlocks.Auditing;
using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Commercial.Pricing.PricingRules.Commands;

// WP-VW-W6 W6-BE-b — MOD-0156 §4.3 / §4.4 rule commands; every one audited (§21).

public sealed record CreatePricingRuleCommand(PricingRuleInput Input)
    : IRequest<Response<PricingRuleDto>>, IAuditableCommand, IAuditDetailsProvider
{
    public AuditDescriptor DescribeAudit() => PricingAudit.Describe(AuditOperation.Create, PricingAudit.PricingRuleEntity, null);

    public void DescribeAuditDetails(AuditDetails details, object? response)
    {
        var dto = (response as Response<PricingRuleDto>)?.Data;
        details.EntityId = dto?.Id;
        details.ObjectVersion = dto?.Version;
        details.Set("legalEntityId", Input?.LegalEntityId).Set("skuId", Input?.SkuId).Set("ruleKey", dto?.RuleKey);
    }
}

/// <summary>Edits a rule DRAFT; with <paramref name="Revision"/> (the <c>/revisions</c> door, <c>revise</c>) it opens a new
/// version draft of an ACTIVE rule instead (an active rule never changes in place).</summary>
public sealed record UpdatePricingRuleCommand(Guid Id, PricingRuleInput Input, bool Revision = false)
    : IRequest<Response<PricingRuleDto>>, IAuditableCommand, IAuditDetailsProvider
{
    public AuditDescriptor DescribeAudit()
        => PricingAudit.Describe(Revision ? AuditOperation.Create : AuditOperation.Update, PricingAudit.PricingRuleEntity, Revision ? null : Id);

    public void DescribeAuditDetails(AuditDetails details, object? response)
    {
        var dto = (response as Response<PricingRuleDto>)?.Data;
        if (Revision)
        {
            details.EntityId = dto?.Id;
        }

        details.ObjectVersion = dto?.Version;
        details.Set("revision", Revision).Set("revisedRuleId", Revision ? Id : null).Set("ruleVersion", dto?.RuleVersion);
    }
}

/// <summary>Opens a RETIRE version draft of an active rule (approved by four eyes like any change).</summary>
public sealed record RequestPricingRuleRetirementCommand(Guid Id)
    : IRequest<Response<PricingRuleDto>>, IAuditableCommand, IAuditDetailsProvider
{
    public AuditDescriptor DescribeAudit() => PricingAudit.Describe(AuditOperation.Create, PricingAudit.PricingRuleEntity, null);

    public void DescribeAuditDetails(AuditDetails details, object? response)
    {
        var dto = (response as Response<PricingRuleDto>)?.Data;
        details.EntityId = dto?.Id;
        details.Set("retiredRuleId", Id).Set("changeKind", dto?.ChangeKind);
    }
}

public sealed record DiscardPricingRuleDraftCommand(Guid Id, int? ExpectedVersion)
    : IRequest<Response<PricingRuleDto>>, IAuditableCommand, IAuditDetailsProvider
{
    public AuditDescriptor DescribeAudit() => PricingAudit.Describe(AuditOperation.LifecycleTransition, PricingAudit.PricingRuleEntity, Id);

    public void DescribeAuditDetails(AuditDetails details, object? response)
        => details.Set("status", (response as Response<PricingRuleDto>)?.Data?.Status);
}

public sealed record SubmitPricingRuleCommand(Guid Id, int? ExpectedVersion)
    : IRequest<Response<PricingRuleDto>>, IAuditableCommand, IAuditDetailsProvider
{
    public AuditDescriptor DescribeAudit() => PricingAudit.Describe(AuditOperation.LifecycleTransition, PricingAudit.PricingRuleEntity, Id);

    public void DescribeAuditDetails(AuditDetails details, object? response)
        => details.Set("status", (response as Response<PricingRuleDto>)?.Data?.Status);
}

public sealed record DecidePricingRuleCommand(Guid Id, PricingRuleDecisionInput Input)
    : IRequest<Response<PricingRuleDto>>, IAuditableCommand, IAuditDetailsProvider
{
    public AuditDescriptor DescribeAudit() => PricingAudit.Describe(AuditOperation.LifecycleTransition, PricingAudit.PricingRuleEntity, Id);

    public void DescribeAuditDetails(AuditDetails details, object? response)
        => details.Set("decision", Input?.Decision).Set("reasonGiven", !string.IsNullOrWhiteSpace(Input?.Reason))
            .Set("status", (response as Response<PricingRuleDto>)?.Data?.Status);
}
