using Diten.BuildingBlocks.Auditing;
using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Commercial.Pricing.PriceLists.Commands;

// WP-VW-W6 W6-BE-b — MOD-0156 price list commands. EVERY one is audited (F8-a; MOD-0156 §21, §20.3 AÇIK-2).

public sealed record CreatePriceListCommand(PriceListInput Input)
    : IRequest<Response<PriceListDto>>, IAuditableCommand, IAuditDetailsProvider
{
    public AuditDescriptor DescribeAudit() => PricingAudit.Describe(AuditOperation.Create, PricingAudit.PriceListEntity, null);

    public void DescribeAuditDetails(AuditDetails details, object? response)
    {
        var dto = (response as Response<PriceListDto>)?.Data;
        details.EntityId = dto?.Id;
        details.ObjectVersion = dto?.Version;
        details.Set("legalEntityId", Input?.LegalEntityId).Set("currency", Input?.Currency).Set("kind", Input?.Kind)
            .Set("validFrom", Input?.ValidFrom?.ToString("yyyy-MM-dd")).Set("items", Input?.Items?.Count ?? 0);
    }
}

public sealed record UpdatePriceListCommand(Guid Id, PriceListInput Input)
    : IRequest<Response<PriceListDto>>, IAuditableCommand, IAuditDetailsProvider
{
    public AuditDescriptor DescribeAudit() => PricingAudit.Describe(AuditOperation.Update, PricingAudit.PriceListEntity, Id);

    public void DescribeAuditDetails(AuditDetails details, object? response)
    {
        details.ObjectVersion = (response as Response<PriceListDto>)?.Data?.Version;
        details.Set("expectedVersion", Input?.ExpectedVersion).Set("itemsUpserted", Input?.Items?.Count ?? 0)
            .Set("itemsRemoved", Input?.RemoveItemIds?.Count ?? 0);
    }
}

public sealed record CreatePriceListVersionCommand(Guid Id, PriceListVersionInput Input)
    : IRequest<Response<PriceListDto>>, IAuditableCommand, IAuditDetailsProvider
{
    public AuditDescriptor DescribeAudit() => PricingAudit.Describe(AuditOperation.Create, PricingAudit.PriceListEntity, null);

    public void DescribeAuditDetails(AuditDetails details, object? response)
    {
        var dto = (response as Response<PriceListDto>)?.Data;
        details.EntityId = dto?.Id;
        details.ObjectVersion = dto?.Version;
        details.Set("previousPriceListId", Id).Set("listVersion", dto?.ListVersion);
    }
}

public sealed record DiscardPriceListCommand(Guid Id, int? ExpectedVersion)
    : IRequest<Response<PriceListDto>>, IAuditableCommand, IAuditDetailsProvider
{
    public AuditDescriptor DescribeAudit() => PricingAudit.Describe(AuditOperation.LifecycleTransition, PricingAudit.PriceListEntity, Id);

    public void DescribeAuditDetails(AuditDetails details, object? response)
        => details.Set("status", (response as Response<PriceListDto>)?.Data?.Status);
}

public sealed record SubmitPriceListCommand(Guid Id, int? ExpectedVersion)
    : IRequest<Response<PriceListDto>>, IAuditableCommand, IAuditDetailsProvider
{
    public AuditDescriptor DescribeAudit() => PricingAudit.Describe(AuditOperation.LifecycleTransition, PricingAudit.PriceListEntity, Id);

    public void DescribeAuditDetails(AuditDetails details, object? response)
        => details.Set("status", (response as Response<PriceListDto>)?.Data?.Status);
}

/// <summary><c>publish</c> | <c>return</c> (a return needs its reason) — by a SECOND person (D13).</summary>
public sealed record DecidePriceListCommand(Guid Id, PriceListDecisionInput Input)
    : IRequest<Response<PriceListDto>>, IAuditableCommand, IAuditDetailsProvider
{
    public AuditDescriptor DescribeAudit() => PricingAudit.Describe(AuditOperation.LifecycleTransition, PricingAudit.PriceListEntity, Id);

    public void DescribeAuditDetails(AuditDetails details, object? response)
        => details.Set("decision", Input?.Decision).Set("reasonGiven", !string.IsNullOrWhiteSpace(Input?.Reason))
            .Set("status", (response as Response<PriceListDto>)?.Data?.Status);
}
