using System.Linq.Expressions;
using FluentValidation;

namespace Diten.CrmService.Application.Features.Account.Validators;

/// <summary>
/// WP-CRM-MOBILE-REQ CRM-ACC-EDIT — the MOD-0149 §18 field rules shared by the create and the update, each with its field
/// message and machine code (one place, so the two commands answer the same codes).
/// </summary>
public static class AccountFieldRules
{
    public static void Apply<T>(AbstractValidator<T> v, Expression<Func<T, string>> name, Expression<Func<T, string>> type,
        Expression<Func<T, string>> status, Expression<Func<T, string?>> address, Expression<Func<T, double?>> latitude,
        Expression<Func<T, double?>> longitude, Expression<Func<T, string?>> responsibleName, Expression<Func<T, string?>> phone,
        Expression<Func<T, string?>> email, Expression<Func<T, string?>> notes, Expression<Func<T, string?>> logo)
    {
        var emailOf = email.Compile();
        var logoOf = logo.Compile();
        v.RuleFor(name).NotEmpty().WithMessage("AccountName is required.").WithErrorCode(AccountErrors.NameRequired)
            .MaximumLength(200).WithMessage("AccountName is at most 200 characters.").WithErrorCode(AccountErrors.NameTooLong);
        v.RuleFor(type).NotEmpty().WithMessage("AccountType is required.").WithErrorCode(AccountErrors.TypeRequired);
        v.RuleFor(status).NotEmpty().WithMessage("Status is required.").WithErrorCode(AccountErrors.StatusRequired);
        v.RuleFor(address).MaximumLength(500).WithMessage("AddressLine is at most 500 characters.").WithErrorCode(AccountErrors.AddressTooLong);
        v.RuleFor(latitude).InclusiveBetween(-90, 90).WithMessage("Latitude is -90..90.").WithErrorCode(AccountErrors.LatitudeInvalid);
        v.RuleFor(longitude).InclusiveBetween(-180, 180).WithMessage("Longitude is -180..180.").WithErrorCode(AccountErrors.LongitudeInvalid);
        v.RuleFor(responsibleName).MaximumLength(200).WithMessage("ResponsiblePersonName is at most 200 characters.")
            .WithErrorCode(AccountErrors.ResponsibleNameTooLong);
        v.RuleFor(phone).MaximumLength(32).WithMessage("ResponsiblePersonPhone is at most 32 characters.").WithErrorCode(AccountErrors.PhoneInvalid);
        v.RuleFor(email).MaximumLength(256).WithMessage("ResponsiblePersonEmail is at most 256 characters.").WithErrorCode(AccountErrors.EmailInvalid)
            .EmailAddress().WithMessage("ResponsiblePersonEmail is not an e-mail address.").WithErrorCode(AccountErrors.EmailInvalid)
            .When(x => !string.IsNullOrWhiteSpace(emailOf(x)));
        v.RuleFor(notes).MaximumLength(2000).WithMessage("Notes is at most 2000 characters.").WithErrorCode(AccountErrors.NotesTooLong);
        v.RuleFor(logo)
            .Matches(AccountLogoRules.DataUriPattern).WithMessage(AccountLogoRules.FormatMessage).WithErrorCode(AccountErrors.LogoInvalid)
            .MaximumLength(AccountLogoRules.MaxLength).WithMessage(AccountLogoRules.SizeMessage).WithErrorCode(AccountErrors.LogoTooLarge)
            .When(x => !string.IsNullOrWhiteSpace(logoOf(x)));
    }
}
