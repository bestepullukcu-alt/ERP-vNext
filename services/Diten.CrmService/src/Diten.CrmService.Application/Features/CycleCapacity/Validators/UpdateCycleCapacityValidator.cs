using Diten.CrmService.Application.Features.CycleCapacity.Commands;
using Diten.CrmService.Domain.Entities;
using FluentValidation;

namespace Diten.CrmService.Application.Features.CycleCapacity.Validators;

/// <summary>The edit's shape checks. There is deliberately no rule for <c>CyclePeriodId</c>: the command does not
/// carry one, because the pin is set once and never moved.</summary>
public sealed class UpdateCycleCapacityValidator : AbstractValidator<UpdateCycleCapacityCommand>
{
    public UpdateCycleCapacityValidator()
    {
        RuleFor(x => x.CycleCapacityId).NotEmpty();
        RuleFor(x => x.DailyWorkMinutes)
            .InclusiveBetween(CycleCapacityLimits.MinDailyWorkMinutes, CycleCapacityLimits.MaxDailyWorkMinutes);
        RuleFor(x => x.PromoProductTime).InclusiveBetween(0, CycleCapacityLimits.MaxMinutesPerVisit);
        RuleFor(x => x.NonPromoProductTime).InclusiveBetween(0, CycleCapacityLimits.MaxMinutesPerVisit);
        RuleFor(x => x.TravelingTime).InclusiveBetween(0, CycleCapacityLimits.MaxMinutesPerDay);
        RuleFor(x => x.ReportDuration).InclusiveBetween(0, CycleCapacityLimits.MaxMinutesPerDay);
        RuleFor(x => x.QuizDuration).InclusiveBetween(0, CycleCapacityLimits.MaxMinutesPerDay);
        RuleFor(x => x.Months).NotEmpty();
        RuleFor(x => x.Description!)
            .MaximumLength(CycleCapacityLimits.MaxDescriptionLength)
            .When(x => x.Description is not null);
        RuleFor(x => x.CalendarCountryCode!)
            .Length(CycleCapacityLimits.CalendarCountryCodeLength)
            .When(x => !string.IsNullOrWhiteSpace(x.CalendarCountryCode));
    }
}
