using FluentValidation;
using Diten.AuthService.Application.Features.Users.Commands;
using Diten.AuthService.Domain.Enums;

namespace Diten.AuthService.Application.Features.Users.Validators;

public sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("E-posta adresi boş bırakılamaz.")
            .EmailAddress().WithMessage("Geçerli bir e-posta adresi giriniz.");

        // Password is optional (invitation flow sends a set-password link instead). Only
        // enforce the length ceiling when a password is actually supplied (self-service create).
        RuleFor(x => x.Password)
            .MaximumLength(128).WithMessage("Şifre en fazla 128 karakter olabilir.")
            .When(x => !string.IsNullOrEmpty(x.Password));

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("Ad boş bırakılamaz.")
            .MaximumLength(100).WithMessage("Ad en fazla 100 karakter olabilir.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Soyad boş bırakılamaz.")
            .MaximumLength(100).WithMessage("Soyad en fazla 100 karakter olabilir.");

        // WP-INFRA-AUTH-ACCOUNT-KIND-01 — when a kind IS supplied it must be a spelled-out enum name. Blank is not
        // an error: it means "leave the account Unknown". The permission check is the handler's (403, not 400).
        RuleFor(x => x.AccountKind)
            .Must(value => SetAccountKindCommandValidator.IsDefinedKindName(value))
            .WithMessage("AccountKind must be one of: " + string.Join(", ", Enum.GetNames<AccountKind>()) + ".")
            .When(x => !string.IsNullOrWhiteSpace(x.AccountKind));
    }
}
