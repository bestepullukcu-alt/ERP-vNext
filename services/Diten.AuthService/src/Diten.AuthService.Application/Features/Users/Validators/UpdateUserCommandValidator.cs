using FluentValidation;
using Diten.AuthService.Application.Features.Users.Commands;
using Diten.AuthService.Application.Features.Users.Services;
using Diten.AuthService.Domain.Enums;

namespace Diten.AuthService.Application.Features.Users.Validators;

public sealed class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.FirstName)
            .NotEmpty().WithErrorCode(UserErrorCodes.FirstNameRequired).WithMessage("Ad boş bırakılamaz.")
            .MaximumLength(UserFieldLimits.NameMaxLength).WithErrorCode(UserErrorCodes.FirstNameTooLong).WithMessage("Ad en fazla 100 karakter olabilir.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithErrorCode(UserErrorCodes.LastNameRequired).WithMessage("Soyad boş bırakılamaz.")
            .MaximumLength(UserFieldLimits.NameMaxLength).WithErrorCode(UserErrorCodes.LastNameTooLong).WithMessage("Soyad en fazla 100 karakter olabilir.");

        // WP-AUTH-USER-KIND-UPDATE-01 — same rule as create: a supplied kind must be a spelled-out enum name. Blank
        // means "leave the kind as it is". The permission check is the handler's (403, not 400).
        RuleFor(x => x.AccountKind)
            .Must(value => SetAccountKindCommandValidator.IsDefinedKindName(value))
            .WithErrorCode(UserErrorCodes.AccountKindInvalid)
            .WithMessage("AccountKind must be one of: " + string.Join(", ", Enum.GetNames<AccountKind>()) + ".")
            .When(x => !string.IsNullOrWhiteSpace(x.AccountKind));
    }
}
