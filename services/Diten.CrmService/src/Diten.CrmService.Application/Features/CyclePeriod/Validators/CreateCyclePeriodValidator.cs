using Diten.CrmService.Application.Features.CyclePeriod.Commands;
using Diten.CrmService.Domain.Entities;
using FluentValidation;

namespace Diten.CrmService.Application.Features.CyclePeriod.Validators;

/// <summary>
/// Cheap shape checks that fail before a handler ever runs. The DEEP rules — code format, the window relation, and the
/// set rules (code uniqueness, sequence uniqueness, the active-overlap ban) — live in
/// <see cref="CyclePeriodValidation"/> and <see cref="Rules.CyclePeriodOverlapRules"/>, because they need the tenant's
/// other rows. Duplicating them here would create two sources of truth that drift.
/// </summary>
public sealed class CreateCyclePeriodValidator : AbstractValidator<CreateCyclePeriodCommand>
{
    public CreateCyclePeriodValidator()
    {
        RuleFor(x => x.CycleCode).NotEmpty().MaximumLength(CyclePeriodLimits.MaxCycleCodeLength);
        RuleFor(x => x.CycleName).NotEmpty().MaximumLength(CyclePeriodLimits.MaxCycleNameLength);
        RuleFor(x => x.Year).InclusiveBetween(CyclePeriodLimits.MinYear, CyclePeriodLimits.MaxYear);
        RuleFor(x => x.SequenceInYear)
            .InclusiveBetween(CyclePeriodLimits.MinSequenceInYear, CyclePeriodLimits.MaxSequenceInYear);
        RuleFor(x => x.BusinessUnitId!)
            .MaximumLength(CyclePeriodLimits.MaxBusinessUnitIdLength)
            .When(x => x.BusinessUnitId is not null);
        RuleFor(x => x.Description!)
            .MaximumLength(CyclePeriodLimits.MaxDescriptionLength)
            .When(x => x.Description is not null);
    }
}
