using FluentValidation;

namespace Diten.PpmService.Application.Features.Initiatives;

public sealed class UpdateInitiativeValidator : AbstractValidator<UpdateInitiativeCommand>
{
    public UpdateInitiativeValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.PortfolioId).NotEqual(Guid.Empty);
        RuleFor(x => x.InitiativeTypeCode).MaximumLength(128);
        RuleFor(x => x.PriorityCode).MaximumLength(128);
        RuleFor(x => x.ExpectedVersion).GreaterThan(0);
        RuleFor(x => x).Must(x => !x.PlannedStartDate.HasValue || !x.PlannedEndDate.HasValue || x.PlannedEndDate >= x.PlannedStartDate)
            .WithMessage("PlannedEndDate cannot precede PlannedStartDate.");
    }
}
