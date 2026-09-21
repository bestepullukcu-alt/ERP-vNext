using Diten.ProcurementService.Application.Features.Sourcing.Commands;
using FluentValidation;

namespace Diten.ProcurementService.Application.Features.Sourcing.Validators;

/// <summary>awardRfxEvent validation (MOD-0145 §12): awardedBidId zorunlu.</summary>
public sealed class AwardRfxEventValidator : AbstractValidator<AwardRfxEventCommand>
{
    public AwardRfxEventValidator()
    {
        RuleFor(x => x.AwardedBidId).NotEmpty().WithMessage("awardedBidId zorunlu");
    }
}
