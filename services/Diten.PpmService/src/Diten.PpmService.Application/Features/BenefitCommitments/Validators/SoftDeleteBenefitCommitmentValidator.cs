using FluentValidation;

namespace Diten.PpmService.Application.Features.BenefitCommitments;

public sealed class SoftDeleteBenefitCommitmentValidator : AbstractValidator<SoftDeleteBenefitCommitmentCommand>
{
    public SoftDeleteBenefitCommitmentValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.ExpectedVersion).GreaterThan(0);
    }
}
