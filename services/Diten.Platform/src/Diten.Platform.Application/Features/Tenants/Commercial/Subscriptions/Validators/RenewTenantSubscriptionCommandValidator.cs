using Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions.Validators;

public sealed class RenewTenantSubscriptionCommandValidator : AbstractValidator<RenewTenantSubscriptionCommand>
{
    public RenewTenantSubscriptionCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.SubscriptionId).NotEmpty();
        RuleFor(x => x.Request.NewPeriodEndUtc).GreaterThan(DateTimeOffset.UtcNow);
    }
}
