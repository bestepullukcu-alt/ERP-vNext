using FluentValidation;
using Diten.AuthService.Application.Features.Users.Commands;

namespace Diten.AuthService.Application.Features.Users.Validators;

public sealed class SetTenantPasswordCommandValidator : AbstractValidator<SetTenantPasswordCommand>
{
    public SetTenantPasswordCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("E-posta adresi boş bırakılamaz.")
            .EmailAddress().WithMessage("Geçerli bir e-posta adresi giriniz.");

        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Doğrulama anahtarı boş bırakılamaz.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("Şifre boş bırakılamaz.")
            .MaximumLength(128).WithMessage("Şifre en fazla 128 karakter olabilir.");
    }
}
