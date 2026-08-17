using Diten.Platform.Application.Features.Quotas.Queries;
using FluentValidation;

namespace Diten.Platform.Application.Features.Quotas.Validators;

public sealed class GetTenantQuotaStatusByKeyQueryValidator : AbstractValidator<GetTenantQuotaStatusByKeyQuery>
{
    public GetTenantQuotaStatusByKeyQueryValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty().WithMessage(QuotaErrorCodes.TenantRequired);
        RuleFor(x => x.QuotaKey)
            .NotEmpty()
            .Must(QuotaKeys.IsKnown)
            .WithMessage(QuotaErrorCodes.KeyUnknown);
    }
}
