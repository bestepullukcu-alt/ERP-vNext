using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.Inventory.Queries;
using Diten.InventoryService.Application.Features.Inventory.Services;
using FluentValidation;

namespace Diten.InventoryService.Application.Features.Inventory.Validators;

/// <summary>Filters must be well-formed; <c>page ≥ 1</c>, <c>pageSize ≤ 200</c> (pack §12).</summary>
public sealed class GetBalanceValidator : AbstractValidator<GetBalanceQuery>
{
    public GetBalanceValidator()
    {
        RuleFor(x => x.ItemId).Must(QueryParameterRules.IsOptionalUuid)
            .WithMessage("itemId must be a UUID.").WithErrorCode(InventoryErrorCodes.InvalidRequest);
        RuleFor(x => x.SkuId).Must(QueryParameterRules.IsOptionalUuid)
            .WithMessage("skuId must be a UUID.").WithErrorCode(InventoryErrorCodes.InvalidRequest);
        RuleFor(x => x.SkuLevel).Must(QueryParameterRules.IsOptionalSkuLevel)
            .WithMessage("skuLevel must be one of Gsku, Lsku, FinishedGood.").WithErrorCode(InventoryErrorCodes.InvalidRequest);
        RuleFor(x => x.StockStatus).Must(QueryParameterRules.IsOptionalStockStatus)
            .WithMessage("stockStatus is not a StockStatus value.").WithErrorCode(InventoryErrorCodes.InvalidRequest);
        RuleFor(x => x.Page).Must(QueryParameterRules.IsValidPage)
            .WithMessage("page must be an integer >= 1.").WithErrorCode(InventoryErrorCodes.InvalidRequest);
        RuleFor(x => x.PageSize).Must(QueryParameterRules.IsValidPageSize)
            .WithMessage($"pageSize must be an integer between 1 and {InventoryContract.MaxPageSize}.")
            .WithErrorCode(InventoryErrorCodes.InvalidRequest);
    }
}
