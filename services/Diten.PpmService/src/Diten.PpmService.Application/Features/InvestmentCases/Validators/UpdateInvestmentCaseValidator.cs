using FluentValidation;

namespace Diten.PpmService.Application.Features.InvestmentCases;

public sealed class UpdateInvestmentCaseValidator : AbstractValidator<UpdateInvestmentCaseCommand>
{
    public UpdateInvestmentCaseValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.ExpectedVersion).GreaterThan(0);
        RuleFor(x => x).Must(x => !x.PlannedStartDate.HasValue || !x.PlannedEndDate.HasValue || x.PlannedEndDate >= x.PlannedStartDate)
            .WithMessage("PlannedEndDate cannot be before PlannedStartDate.");
    }
}
