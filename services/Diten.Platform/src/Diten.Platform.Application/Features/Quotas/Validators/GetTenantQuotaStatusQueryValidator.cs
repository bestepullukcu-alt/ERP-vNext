using Diten.Platform.Application.Features.Quotas.Queries;
using FluentValidation;

namespace Diten.Platform.Application.Features.Quotas.Validators;

public sealed class GetTenantQuotaStatusQueryValidator : AbstractValidator<GetTenantQuotaStatusQuery>
{
    public GetTenantQuotaStatusQueryValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty().WithMessage(QuotaErrorCodes.TenantRequired);
    }
}
