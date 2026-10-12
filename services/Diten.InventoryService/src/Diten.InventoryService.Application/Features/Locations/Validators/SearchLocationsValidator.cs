using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.Locations.Handlers.QueryHandlers;
using Diten.InventoryService.Application.Features.Locations.Queries;
using FluentValidation;

namespace Diten.InventoryService.Application.Features.Locations.Validators;

/// <summary>searchLocations literal parameters (400 <c>INVALID_REQUEST</c>); the enum / id filters are parsed by the handler.</summary>
public sealed class SearchLocationsValidator : AbstractValidator<SearchLocationsQuery>
{
    public SearchLocationsValidator()
    {
        RuleFor(x => x.IsStockable).Must(v => v is null or "true" or "false")
            .WithMessage("isStockable must be true or false.").WithErrorCode(InventoryErrorCodes.InvalidRequest);
        RuleFor(x => x.OrderDir).Must(v => v is null or "asc" or "desc")
            .WithMessage("orderDir must be asc or desc.").WithErrorCode(InventoryErrorCodes.InvalidRequest);
        RuleFor(x => x.Search).Must(v => v is null || v.Length <= SearchLocationsHandler.MaxSearchLength)
            .WithMessage($"search must be at most {SearchLocationsHandler.MaxSearchLength} characters.")
            .WithErrorCode(InventoryErrorCodes.InvalidRequest);
    }
}
