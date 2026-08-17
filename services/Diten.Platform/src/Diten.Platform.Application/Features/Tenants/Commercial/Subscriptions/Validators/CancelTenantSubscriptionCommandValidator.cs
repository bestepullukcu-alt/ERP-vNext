using Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions.Validators;

public sealed class CancelTenantSubscriptionCommandValidator : AbstractValidator<CancelTenantSubscriptionCommand>
{
    public CancelTenantSubscriptionCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.SubscriptionId).NotEmpty();
        RuleFor(x => x.Request.CancellationReason).NotEmpty().MaximumLength(500);
    }
}
