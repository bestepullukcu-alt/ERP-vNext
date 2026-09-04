using FluentValidation;

namespace Diten.PpmService.Application.Features.InvestmentCases;

public sealed class CreateInvestmentCaseValidator : AbstractValidator<CreateInvestmentCaseCommand>
{
    public CreateInvestmentCaseValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.PortfolioId).NotEmpty();
        RuleFor(x => x).Must(x => !x.PlannedStartDate.HasValue || !x.PlannedEndDate.HasValue || x.PlannedEndDate >= x.PlannedStartDate)
            .WithMessage("PlannedEndDate cannot be before PlannedStartDate.");
    }
}
