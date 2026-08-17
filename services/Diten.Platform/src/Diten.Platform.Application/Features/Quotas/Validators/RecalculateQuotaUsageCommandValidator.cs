using Diten.Platform.Application.Features.Quotas.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.Quotas.Validators;

public sealed class RecalculateQuotaUsageCommandValidator : AbstractValidator<RecalculateQuotaUsageCommand>
{
    public RecalculateQuotaUsageCommandValidator()
    {
        RuleFor(x => x.Request.TenantId).NotEmpty().WithMessage(QuotaErrorCodes.TenantRequired);
        RuleFor(x => x.Request.QuotaKey).NotEmpty().Must(QuotaKeys.IsKnown).WithMessage(QuotaErrorCodes.KeyUnknown);
        RuleFor(x => x.Request.Source).NotEmpty();
    }
}
