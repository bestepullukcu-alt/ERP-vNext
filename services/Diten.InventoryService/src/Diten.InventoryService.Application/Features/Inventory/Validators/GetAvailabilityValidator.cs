using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.Inventory.Queries;
using Diten.InventoryService.Application.Features.Inventory.Services;
using FluentValidation;

namespace Diten.InventoryService.Application.Features.Inventory.Validators;

/// <summary>Pack §12: <c>itemId</c> or <c>skuId</c> is required; filters must be well-formed.</summary>
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
    }
}
