using FluentValidation;

namespace Diten.Platform.Application.Features.TenantOrganization.Validators;

internal sealed class OrganizationUnitRequestValidator<T> : AbstractValidator<T>
{
    public OrganizationUnitRequestValidator(Func<T, OrganizationUnitRequest> selector)
    {
        RuleFor(x => selector(x).Code).NotEmpty().MaximumLength(80);
        RuleFor(x => selector(x).Name).NotEmpty().MaximumLength(160);
        RuleFor(x => selector(x).LegalEntityId).NotEmpty();
    }
}

internal sealed class PositionRequestValidator<T> : AbstractValidator<T>
{
    public PositionRequestValidator(Func<T, PositionRequest> selector)
    {
        RuleFor(x => selector(x).Code).NotEmpty().MaximumLength(80);
        RuleFor(x => selector(x).Name).NotEmpty().MaximumLength(160);
        RuleFor(x => selector(x).OrganizationUnitId).NotEmpty();
        RuleFor(x => selector(x).JobTitle).MaximumLength(160);
        // MOD-0288 v1 — FTE is a non-negative headcount fraction (e.g. 0.5, 1.0); enums are parsed with a
        // safe default so they need no validation here.
        RuleFor(x => selector(x).Fte).GreaterThanOrEqualTo(0m).When(x => selector(x).Fte.HasValue);
    }
}

internal sealed class PositionAssignmentRequestValidator<T> : AbstractValidator<T>
{
    public PositionAssignmentRequestValidator(Func<T, PositionAssignmentRequest> selector)
    {
        RuleFor(x => selector(x).PositionId).NotEmpty();
        RuleFor(x => selector(x).UserId).NotEmpty();
        RuleFor(x => selector(x).EffectiveFrom).NotEmpty();
        RuleFor(x => selector(x))
            .Must(x => !x.EffectiveTo.HasValue || x.EffectiveTo.Value > x.EffectiveFrom)
            .WithMessage("EffectiveTo must be greater than EffectiveFrom.");
        RuleFor(x => selector(x).AllocationPercent)
            .InclusiveBetween(0m, 100m)
            .When(x => selector(x).AllocationPercent.HasValue);
        RuleFor(x => selector(x).Notes).MaximumLength(1024);
    }
}
