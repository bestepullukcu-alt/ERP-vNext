using Diten.CrmService.Application.Features.CycleCapacity.Commands;
using Diten.CrmService.Domain.Entities;
using FluentValidation;

namespace Diten.CrmService.Application.Features.CycleCapacity.Validators;

/// <summary>
/// Cheap shape checks that fail before a handler ever runs. The DEEP rules — the non-zero divisor, the day-budget
/// relation, the month-window intersection and the governed country vocabulary — live in
/// <see cref="CycleCapacityValidation"/> and <see cref="Services.CycleCapacityWriteValidator"/>, because they need the
/// pinned period or a reference-data call. Duplicating them here would create two sources of truth that drift.
/// </summary>
public sealed class CreateCycleCapacityValidator : AbstractValidator<CreateCycleCapacityCommand>
{
    public CreateCycleCapacityValidator()
    {
        RuleFor(x => x.CyclePeriodId).NotEmpty();
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
