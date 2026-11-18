using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.Inventory.Queries;
using Diten.InventoryService.Application.Features.Inventory.Services;
using FluentValidation;

namespace Diten.InventoryService.Application.Features.Inventory.Validators;

/// <summary>Pack §12: <c>itemId</c> or <c>skuId</c> is required; filters must be well-formed. MOD-0173-FU03 §12: the <c>lotId</c> rules.</summary>
public sealed class GetAvailabilityValidator : AbstractValidator<GetAvailabilityQuery>
{
    public GetAvailabilityValidator()
    {
        RuleFor(x => x)
            .Must(q => !string.IsNullOrWhiteSpace(q.ItemId) || !string.IsNullOrWhiteSpace(q.SkuId))
            .WithMessage("itemId or skuId is required.")
            .WithErrorCode(InventoryErrorCodes.InvalidRequest);
        RuleFor(x => x.ItemId).Must(QueryParameterRules.IsOptionalUuid)
            .WithMessage("itemId must be a UUID.").WithErrorCode(InventoryErrorCodes.InvalidRequest);
        RuleFor(x => x.SkuId).Must(QueryParameterRules.IsOptionalUuid)
            .WithMessage("skuId must be a UUID.").WithErrorCode(InventoryErrorCodes.InvalidRequest);
        RuleFor(x => x.SkuLevel).Must(QueryParameterRules.IsOptionalSkuLevel)
            .WithMessage("skuLevel must be one of Gsku, Lsku, FinishedGood.").WithErrorCode(InventoryErrorCodes.InvalidRequest);

        // INVENTORY 1.6.0 (MOD-0173-FU03 §12): lotId is a UUID, needs warehouseId and one named SKU, and has no bin pin (Q-02).
        RuleFor(x => x.LotId).Must(QueryParameterRules.IsOptionalUuid)
            .WithMessage("lotId must be a UUID.").WithErrorCode(InventoryErrorCodes.InvalidRequest);
        RuleFor(x => x).Must(q => string.IsNullOrWhiteSpace(q.LotId) || !string.IsNullOrWhiteSpace(q.WarehouseId))
            .WithMessage("lotId needs warehouseId.").WithErrorCode(InventoryErrorCodes.InvalidRequest);
        RuleFor(x => x).Must(q => string.IsNullOrWhiteSpace(q.LotId) || string.IsNullOrWhiteSpace(q.LocationId))
            .WithMessage("lotId and locationId cannot be combined: a reservation is not pinned to a bin.").WithErrorCode(InventoryErrorCodes.InvalidRequest);
        RuleFor(x => x).Must(q => string.IsNullOrWhiteSpace(q.LotId)
                                  || (!string.IsNullOrWhiteSpace(q.ItemId) && !string.IsNullOrWhiteSpace(q.SkuId) && !string.IsNullOrWhiteSpace(q.SkuLevel)))
            .WithMessage("lotId needs itemId, skuId and skuLevel: the scope figures are per SKU.").WithErrorCode(InventoryErrorCodes.InvalidRequest);
    }
}
