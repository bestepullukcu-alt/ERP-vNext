using Diten.Platform.Application.Features.Quotas.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.Quotas.Validators;

public sealed class ReleaseQuotaCommandValidator : AbstractValidator<ReleaseQuotaCommand>
{
    public ReleaseQuotaCommandValidator()
    {
        RuleFor(x => x.Request.TenantId).NotEmpty().WithMessage(QuotaErrorCodes.TenantRequired);
        RuleFor(x => x.Request.QuotaKey).NotEmpty().Must(QuotaKeys.IsKnown).WithMessage(QuotaErrorCodes.KeyUnknown);
        RuleFor(x => x.Request.Amount).GreaterThan(0).WithMessage(QuotaErrorCodes.ReleaseInvalidAmount);
        RuleFor(x => x.Request.Source).NotEmpty();
    }
}
