using FluentValidation;
using Diten.SupplyChainService.Application.Features.Carriers.Commands;
using Diten.SupplyChainService.Domain.Features.Carriers;
namespace Diten.SupplyChainService.Application.Features.Carriers.Validators;
public sealed class CreateCarrierValidator : AbstractValidator<CreateCarrierCommand>
{
    public CreateCarrierValidator()
    {
        RuleFor(x => x.Body.CarrierCode).Must(x => x is { Length: > 0 });
        RuleFor(x => x.Body.DisplayName).Must(x => x is { Length: > 0 });
        RuleFor(x => x.Body.SupportedModes).Must(x => x is { Count: > 0 } && x.All(v => Enum.GetNames<TransportMode>().Contains(v, StringComparer.Ordinal)));
    }
}
