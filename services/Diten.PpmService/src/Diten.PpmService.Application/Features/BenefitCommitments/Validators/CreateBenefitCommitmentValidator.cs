using FluentValidation;

namespace Diten.PpmService.Application.Features.BenefitCommitments;

public sealed class CreateBenefitCommitmentValidator : AbstractValidator<CreateBenefitCommitmentCommand>
{
    public CreateBenefitCommitmentValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.InvestmentCaseId).NotEmpty();
        RuleFor(x => x.TargetDescription).NotEmpty().MaximumLength(1000);
    }
}
