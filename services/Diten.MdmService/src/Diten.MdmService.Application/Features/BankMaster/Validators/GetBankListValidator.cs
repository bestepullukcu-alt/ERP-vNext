using Diten.MdmService.Application.Features.BankMaster.Queries;
using FluentValidation;

namespace Diten.MdmService.Application.Features.BankMaster.Validators;

/// <summary>
/// AC-59 (KARAR D51/4): a search longer than 100 characters (after trimming) is a 400 and the store is never asked. The
/// text is searched as plain text, whatever characters it holds — that is the repository's rule, not a validator's.
/// </summary>
public sealed class GetBankListValidator : AbstractValidator<GetBankListQuery>
{
    public GetBankListValidator()
    {
        RuleFor(x => x.TenantId).Must(tenant => tenant is null).WithMessage(BankValidationMessages.TenantForbidden);
        RuleFor(x => x.Search)
            .Must(search => (search?.Trim().Length ?? 0) <= BankRules.SearchMaxLength)
            .WithMessage(BankValidationMessages.SearchTooLong);
        RuleFor(x => x.Status)
            .Must(status => BankRules.TryParseStatus(status, out _))
            .When(x => !string.IsNullOrWhiteSpace(x.Status))
            .WithMessage(BankValidationMessages.StatusInvalid);
        RuleFor(x => x.CountryCode)
            .Must(country => BankRules.IsValidCountry(BankRules.NormalizeCountry(country)))
            .When(x => !string.IsNullOrWhiteSpace(x.CountryCode))
            .WithMessage(BankValidationMessages.CountryInvalid);
        RuleFor(x => x.SortBy).Must(sort => BankListSort.TryParse(sort, out _)).WithMessage(BankValidationMessages.SortInvalid);
        RuleFor(x => x.SortDir).Must(dir => BankListSort.TryParseDirection(dir, out _)).WithMessage(BankValidationMessages.SortInvalid);
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage(BankValidationMessages.PagingInvalid);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage(BankValidationMessages.PagingInvalid);
    }
}
