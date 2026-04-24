using Diten.Platform.Application.Features.Tenants.Commands;
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
    }

    private static bool BeDomainLike(string value)
    {
        var domain = value.Trim();
        return domain.Contains('.') && !domain.StartsWith('.') && !domain.EndsWith('.');
    }
}
