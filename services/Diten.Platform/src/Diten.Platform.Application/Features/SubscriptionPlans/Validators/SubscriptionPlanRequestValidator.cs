using FluentValidation;

namespace Diten.Platform.Application.Features.SubscriptionPlans.Validators;

public sealed class SubscriptionPlanRequestValidator<T> : AbstractValidator<T>
{
    public SubscriptionPlanRequestValidator(
        Func<T, string?> code,
        Func<T, string?> name,
        Func<T, int?> sortOrder,
        Func<T, decimal?> priceMonthly,
        Func<T, decimal?> priceYearly,
        Func<T, string?> currency,
        Func<T, bool> isTrialPlan,
        Func<T, int?> trialDurationDays,
        Func<T, bool> isDefault,
        Func<T, bool> isActive)
    {
        RuleFor(x => code(x))
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Code is required.")
            .Must(value => SubscriptionPlanCodeNormalizer.Normalize(value).Length is >= 2 and <= 50)
            .WithMessage("Code must be between 2 and 50 characters after canonical normalization.");

        RuleFor(x => name(x))
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200).WithMessage("Name cannot exceed 200 characters.");

        RuleFor(x => sortOrder(x))
            .GreaterThanOrEqualTo(0).When(x => sortOrder(x).HasValue)
            .WithMessage("SortOrder must be greater than or equal to 0.");

        RuleFor(x => priceMonthly(x))
            .GreaterThanOrEqualTo(0).When(x => priceMonthly(x).HasValue)
            .WithMessage("PriceMonthly cannot be negative.");

        RuleFor(x => priceYearly(x))
            .GreaterThanOrEqualTo(0).When(x => priceYearly(x).HasValue)
            .WithMessage("PriceYearly cannot be negative.");

        RuleFor(x => currency(x))
            .NotEmpty()
            .When(x => priceMonthly(x).HasValue || priceYearly(x).HasValue)
            .WithMessage("Currency is required when a price is provided.");

        RuleFor(x => trialDurationDays(x))
            .Cascade(CascadeMode.Stop)
            .NotNull().When(x => isTrialPlan(x)).WithMessage("TrialDurationDays is required when IsTrialPlan is true.")
            .GreaterThan(0).When(x => isTrialPlan(x)).WithMessage("TrialDurationDays must be greater than 0.");

        RuleFor(x => trialDurationDays(x))
            .Must(value => value is null)
            .When(x => !isTrialPlan(x))
            .WithMessage("TrialDurationDays must be null when IsTrialPlan is false.");

        RuleFor(x => isDefault(x))
            .Must((model, value) => !value || isActive(model))
            .WithMessage("Default plan must be active.");
    }
}
