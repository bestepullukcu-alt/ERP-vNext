using Diten.MdmService.Application.Features.BankMaster.Queries;
using FluentValidation;

namespace Diten.MdmService.Application.Features.BankMaster.Validators;

public sealed class GetBankBranchListValidator : AbstractValidator<GetBankBranchListQuery>
{
    public GetBankBranchListValidator()
    {
        RuleFor(x => x.TenantId).Must(tenant => tenant is null).WithMessage(BankValidationMessages.TenantForbidden);
        RuleFor(x => x.Search)
            .Must(search => (search?.Trim().Length ?? 0) <= BankRules.SearchMaxLength)
            .WithMessage(BankValidationMessages.SearchTooLong);
        RuleFor(x => x.Status)
            .Must(status => BankRules.TryParseStatus(status, out _))
            .When(x => !string.IsNullOrWhiteSpace(x.Status))
            .WithMessage(BankValidationMessages.StatusInvalid);
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage(BankValidationMessages.PagingInvalid);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage(BankValidationMessages.PagingInvalid);
    }
}
