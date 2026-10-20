using Diten.BuildingBlocks.Auditing;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Commercial.Pricing.PriceLists;
using MediatR;

namespace Diten.CrmService.Application.Features.Commercial.Pricing.PriceListImports.Commands;

// WP-VW-W6 W6-BE-b — MOD-0156 §4.2 import commands; every one audited (the file's CONTENT is never recorded, §21).

public sealed record UploadPriceListImportCommand(PriceListImportUpload Upload)
    : IRequest<Response<PriceListImportDto>>, IAuditableCommand, IAuditDetailsProvider
{
    public AuditDescriptor DescribeAudit() => PricingAudit.Describe(AuditOperation.Create, PricingAudit.PriceListImportEntity, null);

    public void DescribeAuditDetails(AuditDetails details, object? response)
    {
        var dto = (response as Response<PriceListImportDto>)?.Data;
        details.EntityId = dto?.Id;
        details.ObjectVersion = dto?.Version;
        details.Set("fileName", Upload?.FileName).Set("fileSha256", dto?.FileSha256).Set("rows", dto?.TotalRows).Set("matched", dto?.Matched)
            .Set("unmatched", dto?.Unmatched).Set("retired", dto?.Retired);
    }
}

public sealed record ConvertPriceListImportCommand(Guid Id)
    : IRequest<Response<PriceListDto>>, IAuditableCommand, IAuditDetailsProvider
{
    public AuditDescriptor DescribeAudit() => PricingAudit.Describe(AuditOperation.LifecycleTransition, PricingAudit.PriceListImportEntity, Id);

    public void DescribeAuditDetails(AuditDetails details, object? response)
        => details.Set("priceListId", (response as Response<PriceListDto>)?.Data?.Id);
}

public sealed record DiscardPriceListImportCommand(Guid Id)
    : IRequest<Response<PriceListImportDto>>, IAuditableCommand, IAuditDetailsProvider
{
    public AuditDescriptor DescribeAudit() => PricingAudit.Describe(AuditOperation.LifecycleTransition, PricingAudit.PriceListImportEntity, Id);

    public void DescribeAuditDetails(AuditDetails details, object? response)
        => details.Set("status", (response as Response<PriceListImportDto>)?.Data?.Status);
}
