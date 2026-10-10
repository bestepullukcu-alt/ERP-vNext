using FluentValidation;
using Diten.SupplyChainService.Application.Features.Carriers.Commands;
using Diten.SupplyChainService.Domain.Features.Carriers;
namespace Diten.SupplyChainService.Application.Features.Carriers.Validators;
public sealed class ChangeCarrierStatusValidator : AbstractValidator<ChangeCarrierStatusCommand>
{
    public ChangeCarrierStatusValidator()
    {
        RuleFor(x => x.Body.TargetStatus).Must(x => Enum.GetNames<CarrierStatus>().Contains(x, StringComparer.Ordinal));
        RuleFor(x => x.Body.ReasonCode).NotNull();
    }
}
