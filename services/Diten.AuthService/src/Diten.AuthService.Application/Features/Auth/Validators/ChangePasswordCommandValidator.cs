using System.Collections.Generic;
using FluentValidation;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Features.Auth.Commands;

namespace Diten.AuthService.Application.Features.Auth.Validators;

public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        // Emit stable codes (+ English fallback text) only — never localized strings. The frontend resolves the
        // code to the request culture. See PasswordErrorCodes for the code<->resx contract.
        RuleFor(x => x.CurrentPassword)
            .NotEmpty()
                .WithErrorCode(PasswordErrorCodes.CurrentRequired)
                .WithMessage("Current password is required.");

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
