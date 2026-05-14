using Diten.Platform.Application.Features.Quotas.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.Quotas.Validators;

public sealed class InitializeTenantQuotasCommandValidator : AbstractValidator<InitializeTenantQuotasCommand>
{
    public InitializeTenantQuotasCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty().WithMessage(QuotaErrorCodes.TenantRequired);
    }
}
