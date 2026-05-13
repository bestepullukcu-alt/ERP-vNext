using Diten.Platform.Application.Features.Quotas.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.Quotas.Validators;

public sealed class SyncTenantQuotaLimitsFromSubscriptionCommandValidator : AbstractValidator<SyncTenantQuotaLimitsFromSubscriptionCommand>
{
    public SyncTenantQuotaLimitsFromSubscriptionCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty().WithMessage(QuotaErrorCodes.TenantRequired);
    }
}
