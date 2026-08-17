using Diten.Platform.Application.Features.Tenants.Commands;
using Diten.Platform.Domain.Entities;
using FluentValidation;

namespace Diten.Platform.Application.Features.Tenants.Validators;

public sealed class UpdateTenantCommandValidator : AbstractValidator<UpdateTenantCommand>
{
    public UpdateTenantCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.Request).NotNull();

        When(x => x.Request != null, () =>
        {
            RuleFor(x => x.Request.Name)
                .NotEmpty()
                .MaximumLength(120);

            RuleFor(x => x.Request.DisplayName)
                .NotEmpty()
                .MaximumLength(200);

            RuleFor(x => x.Request.Domain)
                .NotEmpty()
                .MaximumLength(253);

            RuleFor(x => x.Request.Subdomain)
                .MaximumLength(63)
                .Matches("^[a-z0-9-]+$")
                .When(x => !string.IsNullOrWhiteSpace(x.Request.Subdomain));

            RuleFor(x => x.Request.Slug)
                .MaximumLength(80)
                .Matches("^[a-z0-9-]+$")
                .When(x => !string.IsNullOrWhiteSpace(x.Request.Slug));

            RuleFor(x => x.Request.Country)
                .MaximumLength(3)
                .When(x => !string.IsNullOrWhiteSpace(x.Request.Country));

            RuleFor(x => x.Request.DefaultTimezone)
                .MaximumLength(64)
                .When(x => !string.IsNullOrWhiteSpace(x.Request.DefaultTimezone));

            RuleFor(x => x.Request.DefaultLanguage)
                .MaximumLength(10)
                .When(x => !string.IsNullOrWhiteSpace(x.Request.DefaultLanguage));

            RuleFor(x => x.Request.DefaultCurrency)
                .Length(3)
                .When(x => !string.IsNullOrWhiteSpace(x.Request.DefaultCurrency));

            RuleFor(x => x.Request.TenantType)
                .Must(type => !type.HasValue || type is TenantType.Customer or TenantType.Demo or TenantType.Internal)
                .WithMessage("TenantType must be Customer, Demo, or Internal.");
        });
    }
}
