using Diten.BuildingBlocks.CentralAudit;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Commercial.Pricing.PriceLists;
using MediatR;

namespace Diten.CrmService.Application.Features.Commercial.Pricing.PriceListImports.Commands;

// WP-VW-W6 W6-BE-b — MOD-0156 §4.2 import commands; every one audited (the file's CONTENT is never recorded, §21).

public sealed record UploadPriceListImportCommand(PriceListImportUpload Upload)
    : IRequest<Response<PriceListImportDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => CrmCentralAudit.Create(PricingAudit.PriceListImportEntity);
    public Guid? CentralAuditEntityId(object? response) => CrmCentralAudit.IdFrom<PriceListImportDto>(response, dto => dto.Id);
}

public sealed record ConvertPriceListImportCommand(Guid Id)
    : IRequest<Response<PriceListDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => CrmCentralAudit.Lifecycle(PricingAudit.PriceListImportEntity);
    public Guid? CentralAuditEntityId(object? response) => Id;
}

public sealed record DiscardPriceListImportCommand(Guid Id)
    : IRequest<Response<PriceListImportDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => CrmCentralAudit.Lifecycle(PricingAudit.PriceListImportEntity);
    public Guid? CentralAuditEntityId(object? response) => Id;
}
