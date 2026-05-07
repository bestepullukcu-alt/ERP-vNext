using FluentValidation;
using Diten.AuthService.Application.Features.Users.Commands;

namespace Diten.AuthService.Application.Features.Users.Validators;

public sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("E-posta adresi boş bırakılamaz.")
            .EmailAddress().WithMessage("Geçerli bir e-posta adresi giriniz.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Şifre boş bırakılamaz.")
            .MaximumLength(128).WithMessage("Şifre en fazla 128 karakter olabilir.");

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("Ad boş bırakılamaz.")
            .MaximumLength(100).WithMessage("Ad en fazla 100 karakter olabilir.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Soyad boş bırakılamaz.")
            .MaximumLength(100).WithMessage("Soyad en fazla 100 karakter olabilir.");
    }
}
