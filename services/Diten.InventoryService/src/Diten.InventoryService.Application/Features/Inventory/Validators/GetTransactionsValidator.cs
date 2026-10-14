using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.Inventory.Queries;
using Diten.InventoryService.Application.Features.Inventory.Services;
using FluentValidation;

namespace Diten.InventoryService.Application.Features.Inventory.Validators;

/// <summary>Filters must be well-formed; <c>from</c>/<c>to</c> are <c>yyyy-MM-dd</c> with from ≤ to; paging per pack §12.</summary>
public sealed class GetTransactionsValidator : AbstractValidator<GetTransactionsQuery>
{
    public GetTransactionsValidator()
    {
        RuleFor(x => x.ItemId).Must(QueryParameterRules.IsOptionalUuid)
            .WithMessage("itemId must be a UUID.").WithErrorCode(InventoryErrorCodes.InvalidRequest);
        RuleFor(x => x.From).Must(QueryParameterRules.IsOptionalDate)
            .WithMessage("from must be a date (yyyy-MM-dd).").WithErrorCode(InventoryErrorCodes.InvalidRequest);
        RuleFor(x => x.To).Must(QueryParameterRules.IsOptionalDate)
            .WithMessage("to must be a date (yyyy-MM-dd).").WithErrorCode(InventoryErrorCodes.InvalidRequest);
        RuleFor(x => x)
            .Must(q => !WireParsing.TryParseUtcDate(q.From, out var from)
                       || !WireParsing.TryParseUtcDate(q.To, out var to)
                       || from <= to)
            .WithMessage("from must not be after to.").WithErrorCode(InventoryErrorCodes.InvalidRequest);
        RuleFor(x => x.Page).Must(QueryParameterRules.IsValidPage)
            .WithMessage("page must be an integer >= 1.").WithErrorCode(InventoryErrorCodes.InvalidRequest);
        RuleFor(x => x.PageSize).Must(QueryParameterRules.IsValidPageSize)
            .WithMessage($"pageSize must be an integer between 1 and {InventoryContract.MaxPageSize}.")
            .WithErrorCode(InventoryErrorCodes.InvalidRequest);
    }
}
