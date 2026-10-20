using Diten.BuildingBlocks.Auditing;
using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Commercial.PreOrders.Commands;

// WP-VW-W6 W6-BE-a — MOD-0168 §3 commands. EVERY one is audited (F8-a; §20.3 AÇIK-6, §21). Never in the record: the link
// token (plain or hash), the signature image, e-mail addresses.

public sealed record CreatePreOrderCommand(PreOrderInput Input) : IRequest<Response<PreOrderDto>>, IAuditableCommand, IAuditDetailsProvider
{
    public AuditDescriptor DescribeAudit() => PreOrderAudit.Describe(AuditOperation.Create, PreOrderAudit.PreOrderEntity, null);

    public void DescribeAuditDetails(AuditDetails details, object? response)
    {
        var dto = (response as Response<PreOrderDto>)?.Data;
        details.EntityId = dto?.Id;
        details.ObjectVersion = dto?.Version;
        details.Set("code", dto?.Code).Set("pharmacyAccountId", Input?.PharmacyAccountId).Set("depotAccountId", Input?.DepotAccountId)
            .Set("lines", Input?.Lines?.Count ?? 0);
    }
}

public sealed record UpdatePreOrderCommand(Guid Id, PreOrderInput Input) : IRequest<Response<PreOrderDto>>, IAuditableCommand, IAuditDetailsProvider
{
    public AuditDescriptor DescribeAudit() => PreOrderAudit.Describe(AuditOperation.Update, PreOrderAudit.PreOrderEntity, Id);

    public void DescribeAuditDetails(AuditDetails details, object? response)
    {
        details.ObjectVersion = (response as Response<PreOrderDto>)?.Data?.Version;
        details.Set("expectedVersion", Input?.ExpectedVersion).Set("depotAccountId", Input?.DepotAccountId).Set("lines", Input?.Lines?.Count)
            .Set("requestedDeliveryDate", Input?.RequestedDeliveryDate?.ToString("yyyy-MM-dd"));
    }
}

public sealed record DiscardPreOrderCommand(Guid Id, PreOrderVersionInput Input) : IRequest<Response<PreOrderDto>>, IAuditableCommand, IAuditDetailsProvider
{
    public AuditDescriptor DescribeAudit() => PreOrderAudit.Describe(AuditOperation.LifecycleTransition, PreOrderAudit.PreOrderEntity, Id);

    public void DescribeAuditDetails(AuditDetails details, object? response)
        => details.Set("status", (response as Response<PreOrderDto>)?.Data?.Status);
}

public sealed record CapturePreOrderSignatureCommand(Guid Id, PreOrderSignatureInput Input)
    : IRequest<Response<PreOrderDto>>, IAuditableCommand, IAuditDetailsProvider
{
    public AuditDescriptor DescribeAudit() => PreOrderAudit.Describe(AuditOperation.Update, PreOrderAudit.PreOrderEntity, Id);

    public void DescribeAuditDetails(AuditDetails details, object? response)
    {
        var signature = (response as Response<PreOrderDto>)?.Data?.Signature;
        details.Set("binaryRef", signature?.BinaryRef).Set("signerName", signature?.SignerName ?? Input?.SignerName);
    }
}

public sealed record SubmitPreOrderCommand(Guid Id, PreOrderVersionInput Input) : IRequest<Response<PreOrderDto>>, IAuditableCommand, IAuditDetailsProvider
{
    public AuditDescriptor DescribeAudit() => PreOrderAudit.Describe(AuditOperation.LifecycleTransition, PreOrderAudit.PreOrderEntity, Id);

    public void DescribeAuditDetails(AuditDetails details, object? response)
    {
        var dto = (response as Response<PreOrderDto>)?.Data;
        details.ObjectVersion = dto?.Version;
        details.Set("status", dto?.Status).Set("priceListId", dto?.Price?.PriceListId).Set("priceListVersion", dto?.Price?.PriceListVersion)
            .Set("fxRate", dto?.Fx?.Rate).Set("outcome", dto?.Evaluation?.Outcome).Set("fingerprint", dto?.Evaluation?.RuleSetFingerprint)
            .Set("approvalRequired", dto?.ApprovalRequirement?.Required).Set("approvers", dto?.ApprovalRequirement?.ApproverUserIds.Count);
    }
}

public sealed record DecidePreOrderCommand(Guid Id, PreOrderDecisionInput Input) : IRequest<Response<PreOrderDto>>, IAuditableCommand, IAuditDetailsProvider
{
    public AuditDescriptor DescribeAudit() => PreOrderAudit.Describe(AuditOperation.LifecycleTransition, PreOrderAudit.PreOrderEntity, Id);

    public void DescribeAuditDetails(AuditDetails details, object? response)
    {
        var dto = (response as Response<PreOrderDto>)?.Data;
        details.ObjectVersion = dto?.Version;
        details.Set("decision", Input?.Decision).Set("reason", Input?.Reason).Set("status", dto?.Status)
            .Set("channel", string.IsNullOrWhiteSpace(Input?.LinkToken) ? "work-center" : "email-link");
    }
}

public sealed record CancelPreOrderCommand(Guid Id, PreOrderCancelInput Input) : IRequest<Response<PreOrderDto>>, IAuditableCommand, IAuditDetailsProvider
{
    public AuditDescriptor DescribeAudit() => PreOrderAudit.Describe(AuditOperation.LifecycleTransition, PreOrderAudit.PreOrderEntity, Id);

    public void DescribeAuditDetails(AuditDetails details, object? response)
        => details.Set("reason", Input?.Reason).Set("status", (response as Response<PreOrderDto>)?.Data?.Status);
}

public sealed record CopyPreOrderCommand(Guid Id) : IRequest<Response<PreOrderDto>>, IAuditableCommand, IAuditDetailsProvider
{
    public AuditDescriptor DescribeAudit() => PreOrderAudit.Describe(AuditOperation.Create, PreOrderAudit.PreOrderEntity, null);

    public void DescribeAuditDetails(AuditDetails details, object? response)
    {
        var dto = (response as Response<PreOrderDto>)?.Data;
        details.EntityId = dto?.Id;
        details.ObjectVersion = dto?.Version;
        details.Set("copiedFromPreOrderId", Id).Set("code", dto?.Code);
    }
}

/// <summary>The manual retry of a failed transmission (sales operations); the system step after an approval runs inside
/// the decision.</summary>
public sealed record TransmitPreOrderCommand(Guid Id, PreOrderVersionInput Input) : IRequest<Response<PreOrderDto>>, IAuditableCommand, IAuditDetailsProvider
{
    public AuditDescriptor DescribeAudit() => PreOrderAudit.Describe(AuditOperation.Execute, PreOrderAudit.PreOrderEntity, Id);

    public void DescribeAuditDetails(AuditDetails details, object? response)
    {
        var dto = (response as Response<PreOrderDto>)?.Data;
        details.Set("lastStatus", dto?.Transmission?.LastStatus).Set("status", dto?.Status)
            .Set("dispatchIds", dto?.Transmission?.Attempts.LastOrDefault()?.DispatchIds.Count);
    }
}

public sealed record RecordPreOrderDepotResponseCommand(Guid Id, PreOrderDepotResponseInput Input)
    : IRequest<Response<PreOrderDto>>, IAuditableCommand, IAuditDetailsProvider
{
    public AuditDescriptor DescribeAudit() => PreOrderAudit.Describe(AuditOperation.Update, PreOrderAudit.PreOrderEntity, Id);

    public void DescribeAuditDetails(AuditDetails details, object? response)
        => details.Set("depotResponse", Input?.Status).Set("partialLines", Input?.PartialLines?.Count ?? 0);
}

public sealed record UpdatePreOrderSettingsCommand(PreOrderSettingsInput Input)
    : IRequest<Response<PreOrderSettingsDto>>, IAuditableCommand, IAuditDetailsProvider
{
    public AuditDescriptor DescribeAudit() => PreOrderAudit.Describe(AuditOperation.Update, PreOrderAudit.SettingsEntity, null);

    public void DescribeAuditDetails(AuditDetails details, object? response)
    {
        var dto = (response as Response<PreOrderSettingsDto>)?.Data;
        details.ObjectVersion = dto?.Version;
        details.Set("approvalMode", Input?.ApprovalMode).Set("approvalLinkTtlHours", Input?.ApprovalLinkTtlHours)
            .Set("transmissionRetryMax", Input?.TransmissionRetryMax).Set("countryTiers", Input?.PriceTierByCountry?.Count)
            .Set("companyPrefixes", Input?.CompanyPrefixes is { } p ? string.Join(",", p.Select(x => $"{x.LegalEntityId:D}={x.Prefix}")) : null);
    }
}
