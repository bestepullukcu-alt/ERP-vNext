using Diten.Platform.Application.Features.Tenants.Commands;
using Diten.Platform.Domain.Entities;
using FluentValidation;

namespace Diten.Platform.Application.Features.Tenants.Validators;

public sealed class RegisterTenantCommandValidator : AbstractValidator<RegisterTenantCommand>
{
    public RegisterTenantCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(120);

        RuleFor(x => x.Domain)
            .NotEmpty()
            .MaximumLength(255)
            .Must(BeDomainLike)
            .WithMessage("Domain must be a valid host format.");

        RuleFor(x => x.Subdomain)
            .MaximumLength(63)
            .Matches("^[a-zA-Z0-9-]*$")
            .When(x => !string.IsNullOrWhiteSpace(x.Subdomain));

        RuleFor(x => x.Slug)
            .MaximumLength(80)
            .Matches("^[a-z0-9]+(?:-[a-z0-9]+)*$")
            .WithMessage("Slug must be lowercase alphanumeric with hyphens.")
            .When(x => !string.IsNullOrWhiteSpace(x.Slug));

        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.PlanId)
            .NotEmpty()
            .WithMessage("PlanId is required.");

        RuleFor(x => x.TenantType)
            .NotNull()
            .WithMessage("TenantType is required.")
            .IsInEnum()
            .Must(type => type != TenantType.Trial && type != TenantType.Paid)
            .WithMessage("TenantType must be Customer, Demo, or Internal.");

        RuleFor(x => x.DisplayName)
            .MaximumLength(200)
            .When(x => !string.IsNullOrWhiteSpace(x.DisplayName));

        RuleFor(x => x.LegalName)
            .MaximumLength(200)
            .When(x => !string.IsNullOrWhiteSpace(x.LegalName));

        RuleFor(x => x.TaxNumber)
            .MaximumLength(50)
            .When(x => !string.IsNullOrWhiteSpace(x.TaxNumber));

        RuleFor(x => x.Country)
            .MaximumLength(3)
            .Matches("^[A-Za-z]{2,3}$")
            .WithMessage("Country must be a 2 or 3 letter ISO code.")
            .When(x => !string.IsNullOrWhiteSpace(x.Country));

        RuleFor(x => x.Industry)
            .MaximumLength(100)
            .When(x => !string.IsNullOrWhiteSpace(x.Industry));

        RuleFor(x => x.ContactPerson)
            .MaximumLength(120)
            .When(x => !string.IsNullOrWhiteSpace(x.ContactPerson));

        RuleFor(x => x.ContactEmail)
            .MaximumLength(255)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.ContactEmail));

        RuleFor(x => x.ContactPhone)
            .MaximumLength(30)
            .Matches(@"^\+?[0-9\s\-\(\)]+$")
            .WithMessage("ContactPhone must be a valid phone number format.")
            .When(x => !string.IsNullOrWhiteSpace(x.ContactPhone));

        RuleFor(x => x.DefaultTimezone)
            .MaximumLength(50)
            .When(x => !string.IsNullOrWhiteSpace(x.DefaultTimezone));

        RuleFor(x => x.DefaultLanguage)
            .MaximumLength(10)
            .Matches("^[a-z]{2}(-[A-Z]{2})?$")
            .WithMessage("DefaultLanguage must be a valid locale code (e.g. 'en', 'tr', 'en-US').")
            .When(x => !string.IsNullOrWhiteSpace(x.DefaultLanguage));

        RuleFor(x => x.DefaultCurrency)
            .MaximumLength(3)
            .Matches("^[A-Z]{3}$")
            .WithMessage("DefaultCurrency must be a 3 letter ISO currency code (e.g. 'USD', 'EUR', 'TRY').")
            .When(x => !string.IsNullOrWhiteSpace(x.DefaultCurrency));

        RuleFor(x => x.InitialAdmin)
            .NotNull()
            .WithMessage("InitialAdmin.Email is required.");

        When(x => x.InitialAdmin != null, () =>
        {
            RuleFor(x => x.InitialAdmin!.FirstName)
                .MaximumLength(80)
                .When(x => !string.IsNullOrWhiteSpace(x.InitialAdmin!.FirstName));

            RuleFor(x => x.InitialAdmin!.LastName)
                .MaximumLength(80)
                .When(x => !string.IsNullOrWhiteSpace(x.InitialAdmin!.LastName));

            RuleFor(x => x.InitialAdmin!.Email)
                .NotEmpty()
                .WithMessage("InitialAdmin.Email is required.")
                .EmailAddress()
                .MaximumLength(255);

            RuleFor(x => x.InitialAdmin!.Phone)
                .MaximumLength(30)
                .Matches(@"^\+?[0-9\s\-\(\)]+$")
                .WithMessage("InitialAdmin.Phone must be a valid phone number format.")
                .When(x => !string.IsNullOrWhiteSpace(x.InitialAdmin!.Phone));
        });
    }

    private static bool BeDomainLike(string value)
    {
        var domain = value.Trim();
        return domain.Contains('.') && !domain.StartsWith('.') && !domain.EndsWith('.');
    }
}

