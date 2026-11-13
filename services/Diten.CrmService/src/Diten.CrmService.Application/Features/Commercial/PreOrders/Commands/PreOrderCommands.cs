using Diten.BuildingBlocks.CentralAudit;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Commercial.PreOrders.Commands;

// WP-VW-W6 W6-BE-a — MOD-0168 §3 commands. EVERY one is audited (F8-b: ICentralAuditDeclaration; §20.3 AÇIK-6, §21). Never in the record: the link
// token (plain or hash), the signature image, e-mail addresses.

public sealed record CreatePreOrderCommand(PreOrderInput Input) : IRequest<Response<PreOrderDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => CrmCentralAudit.Create(PreOrderAudit.PreOrderEntity);
    public Guid? CentralAuditEntityId(object? response) => CrmCentralAudit.IdFrom<PreOrderDto>(response, dto => dto.Id);
}

public sealed record UpdatePreOrderCommand(Guid Id, PreOrderInput Input) : IRequest<Response<PreOrderDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => CrmCentralAudit.Update(PreOrderAudit.PreOrderEntity);
    public Guid? CentralAuditEntityId(object? response) => Id;
}

public sealed record DiscardPreOrderCommand(Guid Id, PreOrderVersionInput Input) : IRequest<Response<PreOrderDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => CrmCentralAudit.Lifecycle(PreOrderAudit.PreOrderEntity);
    public Guid? CentralAuditEntityId(object? response) => Id;
}

public sealed record CapturePreOrderSignatureCommand(Guid Id, PreOrderSignatureInput Input)
    : IRequest<Response<PreOrderDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => CrmCentralAudit.Update(PreOrderAudit.PreOrderEntity);
    public Guid? CentralAuditEntityId(object? response) => Id;
}

public sealed record SubmitPreOrderCommand(Guid Id, PreOrderVersionInput Input) : IRequest<Response<PreOrderDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => CrmCentralAudit.Lifecycle(PreOrderAudit.PreOrderEntity);
    public Guid? CentralAuditEntityId(object? response) => Id;
}

public sealed record DecidePreOrderCommand(Guid Id, PreOrderDecisionInput Input) : IRequest<Response<PreOrderDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => CrmCentralAudit.Lifecycle(PreOrderAudit.PreOrderEntity);
    public Guid? CentralAuditEntityId(object? response) => Id;
}

public sealed record CancelPreOrderCommand(Guid Id, PreOrderCancelInput Input) : IRequest<Response<PreOrderDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => CrmCentralAudit.Lifecycle(PreOrderAudit.PreOrderEntity);
    public Guid? CentralAuditEntityId(object? response) => Id;
}

public sealed record CopyPreOrderCommand(Guid Id) : IRequest<Response<PreOrderDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => CrmCentralAudit.Create(PreOrderAudit.PreOrderEntity);
    public Guid? CentralAuditEntityId(object? response) => CrmCentralAudit.IdFrom<PreOrderDto>(response, dto => dto.Id);
}

/// <summary>The manual retry of a failed transmission (sales operations); the system step after an approval runs inside
/// the decision.</summary>
public sealed record TransmitPreOrderCommand(Guid Id, PreOrderVersionInput Input) : IRequest<Response<PreOrderDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => CrmCentralAudit.Execute(PreOrderAudit.PreOrderEntity);
    public Guid? CentralAuditEntityId(object? response) => Id;
}

public sealed record RecordPreOrderDepotResponseCommand(Guid Id, PreOrderDepotResponseInput Input)
    : IRequest<Response<PreOrderDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => CrmCentralAudit.Update(PreOrderAudit.PreOrderEntity);
    public Guid? CentralAuditEntityId(object? response) => Id;
}

public sealed record UpdatePreOrderSettingsCommand(PreOrderSettingsInput Input)
    : IRequest<Response<PreOrderSettingsDto>>, ICentralAuditDeclaration
{
    // One settings document per tenant: no entity id. What changed is the settings history (MOD-0168 §21, B0a).
    public CentralAuditMetadata CentralAudit => CrmCentralAudit.Update(PreOrderAudit.SettingsEntity);
    public Guid? CentralAuditEntityId(object? response) => null;
}
