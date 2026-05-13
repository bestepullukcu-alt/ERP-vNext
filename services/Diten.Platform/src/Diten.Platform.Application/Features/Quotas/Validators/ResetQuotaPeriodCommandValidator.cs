using Diten.Platform.Application.Features.Quotas.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.Quotas.Validators;

public sealed class ResetQuotaPeriodCommandValidator : AbstractValidator<ResetQuotaPeriodCommand>
{
    public ResetQuotaPeriodCommandValidator()
    {
        RuleFor(x => x.Request.TenantId).NotEmpty().WithMessage(QuotaErrorCodes.TenantRequired);
        RuleFor(x => x.Request.QuotaKey)
            .NotEmpty()
            .Must(QuotaKeys.IsKnown)
            .WithMessage(QuotaErrorCodes.KeyUnknown)
            .Must(QuotaKeys.IsResettable)
            .WithMessage(QuotaErrorCodes.PeriodResetNotAllowed);
        RuleFor(x => x.Request.PeriodEnd).GreaterThan(x => x.Request.PeriodStart).WithMessage(QuotaErrorCodes.PeriodResetNotAllowed);
        RuleFor(x => x.Request.Source).NotEmpty();
    }
}
