using Diten.CrmService.Application.Features.CyclePeriod.Commands;
using Diten.CrmService.Domain.Entities;
using FluentValidation;

namespace Diten.CrmService.Application.Features.CyclePeriod.Validators;

/// <summary>Shape checks for an edit. CycleCode is absent from the command on purpose: the stable business key is
/// never renamed, so there is nothing to validate.</summary>
public sealed class UpdateCyclePeriodValidator : AbstractValidator<UpdateCyclePeriodCommand>
{
    public UpdateCyclePeriodValidator()
    {
        RuleFor(x => x.CyclePeriodId).NotEmpty();
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
