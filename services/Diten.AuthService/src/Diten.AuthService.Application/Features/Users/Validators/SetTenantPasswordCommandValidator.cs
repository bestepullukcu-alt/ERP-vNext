using System.Collections.Generic;
using FluentValidation;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Features.Users.Commands;

namespace Diten.AuthService.Application.Features.Users.Validators;

public sealed class SetTenantPasswordCommandValidator : AbstractValidator<SetTenantPasswordCommand>
{
    public SetTenantPasswordCommandValidator()
    {
        // Emit stable codes (+ English fallback text) only — never localized strings. The email/token here come
        // from the emailed redemption link, so a malformed-email path is near-impossible; that one rule keeps an
        // English fallback (no code) rather than expanding the approved code set.
        RuleFor(x => x.Email)
            .NotEmpty()
                .WithErrorCode(PasswordErrorCodes.ResetEmailRequired)
                .WithMessage("Email is required.")
            .EmailAddress()
                .WithMessage("A valid email address is required.");

        RuleFor(x => x.Token)
            .NotEmpty()
                .WithErrorCode(PasswordErrorCodes.ResetTokenRequired)
                .WithMessage("Reset token is required.");

        RuleFor(x => x.NewPassword)
            .NotEmpty()
                .WithErrorCode(PasswordErrorCodes.NewRequired)
                .WithMessage("New password is required.")
            .MaximumLength(128)
                .WithErrorCode(PasswordErrorCodes.TooLong)
                .WithState(_ => new Dictionary<string, string> { ["maxLength"] = "128" })
                .WithMessage("Password can be at most 128 characters.");
    }
}
