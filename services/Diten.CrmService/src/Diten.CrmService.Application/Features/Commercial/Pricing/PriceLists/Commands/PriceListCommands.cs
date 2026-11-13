using Diten.BuildingBlocks.CentralAudit;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Commercial.Pricing.PriceLists.Commands;

// WP-VW-W6 W6-BE-b — MOD-0156 price list commands. EVERY one is audited (F8-b: ICentralAuditDeclaration; MOD-0156 §21, §20.3 AÇIK-2).

public sealed record CreatePriceListCommand(PriceListInput Input)
    : IRequest<Response<PriceListDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => CrmCentralAudit.Create(PricingAudit.PriceListEntity);
    public Guid? CentralAuditEntityId(object? response) => CrmCentralAudit.IdFrom<PriceListDto>(response, dto => dto.Id);
}

public sealed record UpdatePriceListCommand(Guid Id, PriceListInput Input)
    : IRequest<Response<PriceListDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => CrmCentralAudit.Update(PricingAudit.PriceListEntity);
    public Guid? CentralAuditEntityId(object? response) => Id;
}

public sealed record CreatePriceListVersionCommand(Guid Id, PriceListVersionInput Input)
    : IRequest<Response<PriceListDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => CrmCentralAudit.Create(PricingAudit.PriceListEntity);
    public Guid? CentralAuditEntityId(object? response) => CrmCentralAudit.IdFrom<PriceListDto>(response, dto => dto.Id);
}

public sealed record DiscardPriceListCommand(Guid Id, int? ExpectedVersion)
    : IRequest<Response<PriceListDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => CrmCentralAudit.Lifecycle(PricingAudit.PriceListEntity);
    public Guid? CentralAuditEntityId(object? response) => Id;
}

public sealed record SubmitPriceListCommand(Guid Id, int? ExpectedVersion)
    : IRequest<Response<PriceListDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => CrmCentralAudit.Lifecycle(PricingAudit.PriceListEntity);
    public Guid? CentralAuditEntityId(object? response) => Id;
}

/// <summary><c>publish</c> | <c>return</c> (a return needs its reason) — by a SECOND person (D13).</summary>
public sealed record DecidePriceListCommand(Guid Id, PriceListDecisionInput Input)
    : IRequest<Response<PriceListDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => CrmCentralAudit.Lifecycle(PricingAudit.PriceListEntity);
    public Guid? CentralAuditEntityId(object? response) => Id;
}
