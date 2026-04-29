using Diten.DevEnablementService.Application.Features.GoldenReferenceCompact.Commands;
using FluentValidation;

namespace Diten.DevEnablementService.Application.Features.GoldenReferenceCompact.Validators;

public sealed class CreateGoldenReferenceCompactValidator : AbstractValidator<CreateGoldenReferenceCompactCommand>
{
    public CreateGoldenReferenceCompactValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ReferenceType).MaximumLength(80);
        RuleFor(x => x.Category).MaximumLength(120);
        RuleFor(x => x.GroupKey).MaximumLength(120);
        RuleFor(x => x.SourceSystem).MaximumLength(120);
        RuleFor(x => x.Owner).MaximumLength(120);
        RuleFor(x => x.Version).MaximumLength(40);
        RuleFor(x => x.Priority).InclusiveBetween(0, 100);
        RuleFor(x => x.ExpirationDate)
            .GreaterThanOrEqualTo(x => x.EffectiveDate)
            .When(x => x.EffectiveDate.HasValue && x.ExpirationDate.HasValue);
    }
}
