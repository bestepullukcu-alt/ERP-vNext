using Diten.Platform.Application.Features.SubscriptionPlans.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.SubscriptionPlans.Validators;

public sealed class CreateSubscriptionPlanCommandValidator : AbstractValidator<CreateSubscriptionPlanCommand>
{
    public CreateSubscriptionPlanCommandValidator()
    {
        Include(new SubscriptionPlanRequestValidator<CreateSubscriptionPlanCommand>(
            x => x.Request.Code,
            x => x.Request.Name,
            x => x.Request.SortOrder,
            x => x.Request.PriceMonthly,
            x => x.Request.PriceYearly,
            x => x.Request.Currency,
            x => x.Request.IsTrialPlan,
            x => x.Request.TrialDurationDays,
            x => x.Request.IsDefault,
            x => x.Request.IsActive));
    }
}

