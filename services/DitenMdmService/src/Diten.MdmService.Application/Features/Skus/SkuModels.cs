using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Application.Features.Skus;

public abstract class SkuUpsertRequestBase
{
    public string Code { get; set; } = string.Empty;
    public Guid ItemId { get; set; }
    public Guid CompositionId { get; set; }
    public int CompositionVersion { get; set; }
    public int CompositionRevision { get; set; }
    public string PackagingForm { get; set; } = string.Empty;
    public decimal PackagingQuantity { get; set; }
    public string? Barcode { get; set; }
    public Guid LifecycleStateId { get; set; }
}

public class SkuListItemDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;

    public Guid CompositionId { get; set; }
    public string CompositionCode { get; set; } = string.Empty;
    public string CompositionName { get; set; } = string.Empty;
    public string CompositionVersionLabel { get; set; } = string.Empty;

    public string Packaging { get; set; } = string.Empty;
    public string? Barcode { get; set; }

    public Guid LifecycleStateId { get; set; }
    public string LifecycleStateCode { get; set; } = string.Empty;
    public string LifecycleState { get; set; } = string.Empty;
}

public sealed class SkuDetailDto : SkuListItemDto
{
    public string PackagingForm { get; set; } = string.Empty;
    public decimal PackagingQuantity { get; set; }
    public int CompositionVersion { get; set; }
    public int CompositionRevision { get; set; }
}

internal static class SkuMapping
{
    public static SkuListItemDto ToListDto(
        Sku entity,
        string itemCode,
        string itemName,
        string compositionCode,
        string compositionName,
        string lifecycleStateCode,
        string lifecycleStateName)
    {
        return new SkuListItemDto
        {
            Id = entity.Id,
            Code = entity.Code,
            ItemId = entity.ItemId,
            ItemCode = itemCode,
            ItemName = itemName,
            CompositionId = entity.CompositionId,
            CompositionCode = compositionCode,
            CompositionName = compositionName,
            CompositionVersionLabel = entity.CompositionVersion.DisplayName,
            Packaging = $"{entity.Packaging.Quantity} {entity.Packaging.Form}",
            Barcode = entity.Barcode,
            LifecycleStateId = entity.LifecycleStateId,
            LifecycleStateCode = lifecycleStateCode,
            LifecycleState = lifecycleStateName
        };
    }

    public static SkuDetailDto ToDetailDto(
        Sku entity,
        string itemCode,
        string itemName,
        string compositionCode,
        string compositionName,
        string lifecycleStateCode,
        string lifecycleStateName)
    {
        var listDto = ToListDto(entity, itemCode, itemName, compositionCode, compositionName, lifecycleStateCode, lifecycleStateName);

        return new SkuDetailDto
        {
            Id = listDto.Id,
            Code = listDto.Code,
            ItemId = listDto.ItemId,
            ItemCode = listDto.ItemCode,
            ItemName = listDto.ItemName,
            CompositionId = listDto.CompositionId,
            CompositionCode = listDto.CompositionCode,
            CompositionName = listDto.CompositionName,
            CompositionVersionLabel = listDto.CompositionVersionLabel,
            Packaging = listDto.Packaging,
            Barcode = listDto.Barcode,
            LifecycleStateId = listDto.LifecycleStateId,
            LifecycleStateCode = listDto.LifecycleStateCode,
            LifecycleState = listDto.LifecycleState,
            PackagingForm = entity.Packaging.Form,
            PackagingQuantity = entity.Packaging.Quantity,
            CompositionVersion = entity.CompositionVersion.VersionNo,
            CompositionRevision = 0
        };
    }
}
