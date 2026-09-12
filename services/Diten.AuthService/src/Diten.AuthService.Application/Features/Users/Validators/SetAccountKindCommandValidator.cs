using Diten.AuthService.Application.Features.Users.Commands;
using Diten.AuthService.Domain.Enums;
using FluentValidation;

namespace Diten.AuthService.Application.Features.Users.Validators;

/// <summary>
/// WP-INFRA-AUTH-ACCOUNT-KIND-01 — the kind must be one of the enum NAMES (case-insensitive). A number ("1"), a
/// blank, or an unknown word is refused here, so the handler only ever sees a defined <see cref="AccountKind"/>.
/// Surfaces as the service's standard validator 400 — GlobalExceptionHandler's ProblemDetails (title "Validation
/// failed", detail = the message), because ValidationBehavior is the OUTERMOST pipeline behavior in AuthService;
/// the handler's own refusals (404, permission) answer inside the Response envelope.
/// </summary>
public sealed class SetAccountKindCommandValidator : AbstractValidator<SetAccountKindCommand>
{
    public const string KindMessage = "Kind must be one of: Unknown, Human, Service.";

    public SetAccountKindCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("UserId is required.");

        RuleFor(x => x.Kind)
            .NotEmpty().WithMessage(KindMessage)
            .Must(IsDefinedKindName).WithMessage(KindMessage);
    }

    /// <summary>True only for a spelled-out enum name — "1" parses with Enum.TryParse but is not a name, so it is refused.</summary>
    public static bool IsDefinedKindName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = value.Trim();
        return Enum.GetNames<AccountKind>().Any(name => string.Equals(name, text, StringComparison.OrdinalIgnoreCase));
    }
}
