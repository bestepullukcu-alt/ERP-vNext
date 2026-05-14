using Diten.Platform.Application.Features.Quotas.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.Quotas.Validators;

public sealed class TryConsumeQuotaCommandValidator : AbstractValidator<TryConsumeQuotaCommand>
{
    public TryConsumeQuotaCommandValidator()
    {
        RuleFor(x => x.Request.TenantId).NotEmpty().WithMessage(QuotaErrorCodes.TenantRequired);
        RuleFor(x => x.Request.QuotaKey).NotEmpty().Must(QuotaKeys.IsKnown).WithMessage(QuotaErrorCodes.KeyUnknown);
        RuleFor(x => x.Request.Amount).GreaterThan(0).WithMessage(QuotaErrorCodes.LimitExceeded);
        RuleFor(x => x.Request.Source).NotEmpty();
    }
}
