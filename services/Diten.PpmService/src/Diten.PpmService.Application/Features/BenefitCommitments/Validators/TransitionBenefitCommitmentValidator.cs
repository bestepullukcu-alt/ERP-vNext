using FluentValidation;

namespace Diten.PpmService.Application.Features.BenefitCommitments;

public sealed class TransitionBenefitCommitmentValidator : AbstractValidator<TransitionBenefitCommitmentLifecycleCommand>
{
    public TransitionBenefitCommitmentValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.TargetState).IsInEnum();
        RuleFor(x => x.ExpectedVersion).GreaterThan(0);
    }
}
