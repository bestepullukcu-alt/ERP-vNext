using System.Text.RegularExpressions;
using Diten.CrmService.Application.Features.Contact.Commands;
using FluentValidation;

namespace Diten.CrmService.Application.Features.Contact.Validators;

/// <summary>Shared shape rules for Contact commands (MOD-0150 location + PII hardening).</summary>
internal static class ContactFieldRules
{
    // Dialing code like "+90" / "+1" / "90". Digits, optional leading '+', up to 5 digits.
    private static readonly Regex PhoneCountryCodePattern = new(@"^\+?\d{1,5}$", RegexOptions.Compiled);

    // BCP-47-ish language tag: "tr", "en-US". Loose, length-bounded.
    private static readonly Regex PreferredLanguagePattern = new(@"^[A-Za-z]{2,3}(-[A-Za-z0-9]{2,8})?$", RegexOptions.Compiled);

    public static bool IsValidPhoneCountryCode(string value) => PhoneCountryCodePattern.IsMatch(value);
    public static bool IsValidPreferredLanguage(string value) => PreferredLanguagePattern.IsMatch(value);
}

public sealed class CreateContactValidator : AbstractValidator<CreateContactCommand>
{
    public CreateContactValidator()
    {
        // At least one of FirstName / LastName is required; both shape-limited.
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.FirstName) || !string.IsNullOrWhiteSpace(x.LastName))
            .WithMessage("At least one of FirstName or LastName is required.")
            .WithName("Name");
        RuleFor(x => x.FirstName!).MaximumLength(120).When(x => x.FirstName is not null);
        RuleFor(x => x.LastName!).MaximumLength(120).When(x => x.LastName is not null);
        RuleFor(x => x.DisplayName!).MaximumLength(200).When(x => !string.IsNullOrWhiteSpace(x.DisplayName));
        RuleFor(x => x.ContactType).NotEmpty();
        RuleFor(x => x.Status).NotEmpty();
        RuleFor(x => x.ProfessionalTitle!).MaximumLength(120).When(x => x.ProfessionalTitle is not null);
        RuleFor(x => x.Specialty!).MaximumLength(120).When(x => x.Specialty is not null);
        RuleFor(x => x.Department!).MaximumLength(120).When(x => x.Department is not null);
        RuleFor(x => x.Phone!).MaximumLength(32).When(x => x.Phone is not null);
        RuleFor(x => x.Email!)
            .MaximumLength(256).EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Notes!).MaximumLength(2000).When(x => x.Notes is not null);
        RuleFor(x => x.ExternalReference!.ExternalId)
            .NotEmpty().WithMessage("ExternalReference.ExternalId is required when an external reference is supplied.")
            .When(x => x.ExternalReference is not null);

        // MOD-0150 Contact Location Hardening — optional location fields (shape only; codes validated vs MOD-0048 in the handler).
        RuleFor(x => x.CountryRef!).MaximumLength(64).When(x => x.CountryRef is not null);
        RuleFor(x => x.CityRef!).MaximumLength(64).When(x => x.CityRef is not null);
        RuleFor(x => x.DistrictRef!).MaximumLength(64).When(x => x.DistrictRef is not null);
        RuleFor(x => x.Gender!).MaximumLength(32).When(x => x.Gender is not null);
        // Avatar: optional small base64 image data-URI. Bounded length (~525KB) + must be an image data-URI.
        RuleFor(x => x.PhotoDataUri!)
            .MaximumLength(700_000).WithMessage("The photo is too large; please use a smaller image.")
            .Must(v => v.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase)).WithMessage("The photo must be an image.")
            .When(x => !string.IsNullOrWhiteSpace(x.PhotoDataUri));
        RuleFor(x => x.AddressLine!).MaximumLength(256).When(x => x.AddressLine is not null);
        RuleFor(x => x.PostalCode!).MaximumLength(16).When(x => x.PostalCode is not null);
        RuleFor(x => x.PreferredLanguage!)
            .Must(ContactFieldRules.IsValidPreferredLanguage).WithMessage("PreferredLanguage is not a valid language tag.")
            .When(x => !string.IsNullOrWhiteSpace(x.PreferredLanguage));
        RuleFor(x => x.PhoneCountryCode!)
            .Must(ContactFieldRules.IsValidPhoneCountryCode).WithMessage("PhoneCountryCode is not a valid dialing code.")
            .When(x => !string.IsNullOrWhiteSpace(x.PhoneCountryCode));
    }
}

public sealed class UpdateContactValidator : AbstractValidator<UpdateContactCommand>
{
    public UpdateContactValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.FirstName) || !string.IsNullOrWhiteSpace(x.LastName))
            .WithMessage("At least one of FirstName or LastName is required.")
            .WithName("Name");
        RuleFor(x => x.FirstName!).MaximumLength(120).When(x => x.FirstName is not null);
        RuleFor(x => x.LastName!).MaximumLength(120).When(x => x.LastName is not null);
        RuleFor(x => x.DisplayName!).MaximumLength(200).When(x => !string.IsNullOrWhiteSpace(x.DisplayName));
        RuleFor(x => x.ContactType).NotEmpty();
        RuleFor(x => x.Status).NotEmpty();
        RuleFor(x => x.ProfessionalTitle!).MaximumLength(120).When(x => x.ProfessionalTitle is not null);
        RuleFor(x => x.Specialty!).MaximumLength(120).When(x => x.Specialty is not null);
        RuleFor(x => x.Department!).MaximumLength(120).When(x => x.Department is not null);
        RuleFor(x => x.Phone!).MaximumLength(32).When(x => x.Phone is not null);
        RuleFor(x => x.Email!)
            .MaximumLength(256).EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Notes!).MaximumLength(2000).When(x => x.Notes is not null);

        // MOD-0150 Contact Location Hardening — optional location fields.
        RuleFor(x => x.CountryRef!).MaximumLength(64).When(x => x.CountryRef is not null);
        RuleFor(x => x.CityRef!).MaximumLength(64).When(x => x.CityRef is not null);
        RuleFor(x => x.DistrictRef!).MaximumLength(64).When(x => x.DistrictRef is not null);
        RuleFor(x => x.Gender!).MaximumLength(32).When(x => x.Gender is not null);
        // Avatar: optional small base64 image data-URI. Bounded length (~525KB) + must be an image data-URI.
        RuleFor(x => x.PhotoDataUri!)
            .MaximumLength(700_000).WithMessage("The photo is too large; please use a smaller image.")
            .Must(v => v.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase)).WithMessage("The photo must be an image.")
            .When(x => !string.IsNullOrWhiteSpace(x.PhotoDataUri));
        RuleFor(x => x.AddressLine!).MaximumLength(256).When(x => x.AddressLine is not null);
        RuleFor(x => x.PostalCode!).MaximumLength(16).When(x => x.PostalCode is not null);
        RuleFor(x => x.PreferredLanguage!)
            .Must(ContactFieldRules.IsValidPreferredLanguage).WithMessage("PreferredLanguage is not a valid language tag.")
            .When(x => !string.IsNullOrWhiteSpace(x.PreferredLanguage));
        RuleFor(x => x.PhoneCountryCode!)
            .Must(ContactFieldRules.IsValidPhoneCountryCode).WithMessage("PhoneCountryCode is not a valid dialing code.")
            .When(x => !string.IsNullOrWhiteSpace(x.PhoneCountryCode));
    }
}
