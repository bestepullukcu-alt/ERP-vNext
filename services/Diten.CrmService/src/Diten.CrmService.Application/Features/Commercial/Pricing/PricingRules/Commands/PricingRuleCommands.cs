using Diten.BuildingBlocks.CentralAudit;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Commercial.Pricing.PricingRules.Commands;

// WP-VW-W6 W6-BE-b — MOD-0156 §4.3 / §4.4 rule commands; every one audited (§21).

public sealed record CreatePricingRuleCommand(PricingRuleInput Input)
    : IRequest<Response<PricingRuleDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => CrmCentralAudit.Create(PricingAudit.PricingRuleEntity);
    public Guid? CentralAuditEntityId(object? response) => CrmCentralAudit.IdFrom<PricingRuleDto>(response, dto => dto.Id);
}

/// <summary>Edits a rule DRAFT; with <paramref name="Revision"/> (the <c>/revisions</c> door, <c>revise</c>) it opens a new
/// version draft of an ACTIVE rule instead (an active rule never changes in place).</summary>
public sealed record UpdatePricingRuleCommand(Guid Id, PricingRuleInput Input, bool Revision = false)
    : IRequest<Response<PricingRuleDto>>, ICentralAuditDeclaration
{
    // A revision opens a NEW version draft (a create, the id comes back in the response); otherwise the draft is edited in place.
    public CentralAuditMetadata CentralAudit
        => Revision ? CrmCentralAudit.Create(PricingAudit.PricingRuleEntity) : CrmCentralAudit.Update(PricingAudit.PricingRuleEntity);
    public Guid? CentralAuditEntityId(object? response)
        => Revision ? CrmCentralAudit.IdFrom<PricingRuleDto>(response, dto => dto.Id) : Id;
}

/// <summary>Opens a RETIRE version draft of an active rule (approved by four eyes like any change).</summary>
public sealed record RequestPricingRuleRetirementCommand(Guid Id)
    : IRequest<Response<PricingRuleDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => CrmCentralAudit.Create(PricingAudit.PricingRuleEntity);
    public Guid? CentralAuditEntityId(object? response) => CrmCentralAudit.IdFrom<PricingRuleDto>(response, dto => dto.Id);
}

public sealed record DiscardPricingRuleDraftCommand(Guid Id, int? ExpectedVersion)
    : IRequest<Response<PricingRuleDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => CrmCentralAudit.Lifecycle(PricingAudit.PricingRuleEntity);
    public Guid? CentralAuditEntityId(object? response) => Id;
}

public sealed record SubmitPricingRuleCommand(Guid Id, int? ExpectedVersion)
    : IRequest<Response<PricingRuleDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => CrmCentralAudit.Lifecycle(PricingAudit.PricingRuleEntity);
    public Guid? CentralAuditEntityId(object? response) => Id;
}

public sealed record DecidePricingRuleCommand(Guid Id, PricingRuleDecisionInput Input)
    : IRequest<Response<PricingRuleDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => CrmCentralAudit.Lifecycle(PricingAudit.PricingRuleEntity);
    public Guid? CentralAuditEntityId(object? response) => Id;
}
