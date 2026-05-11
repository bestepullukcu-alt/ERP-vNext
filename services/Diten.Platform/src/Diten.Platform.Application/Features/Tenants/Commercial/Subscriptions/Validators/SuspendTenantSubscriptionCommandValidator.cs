using Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions.Validators;

public sealed class SuspendTenantSubscriptionCommandValidator : AbstractValidator<SuspendTenantSubscriptionCommand>
{
    public SuspendTenantSubscriptionCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.SubscriptionId).NotEmpty();
        RuleFor(x => x.Request.Reason).NotEmpty().MaximumLength(500);
    }
}
