using Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions.Validators;

public sealed class ActivateTenantSubscriptionCommandValidator : AbstractValidator<ActivateTenantSubscriptionCommand>
{
    public ActivateTenantSubscriptionCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.SubscriptionId).NotEmpty();
        RuleFor(x => x.Request.CurrentPeriodStartUtc).NotEmpty();
        RuleFor(x => x.Request.CurrentPeriodEndUtc).GreaterThan(x => x.Request.CurrentPeriodStartUtc);
    }
}
