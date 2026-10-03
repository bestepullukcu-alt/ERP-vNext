using FluentValidation;
using Diten.AuthService.Application.Features.Roles.Commands;

namespace Diten.AuthService.Application.Features.Roles.Validators;

public sealed class CreateRoleCommandValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleCommandValidator()
    {
        // The limits are RoleFieldLimits' — the sentences read the same constants, so a limit and the number in its
        // sentence cannot drift apart. (The sentences are the server's fallback; the screens say the coded ones.)
        RuleFor(x => x.Name)
            .Must(RoleFieldRules.HasText).WithMessage("Rol adı boş bırakılamaz.").WithErrorCode(RoleErrorCodes.NameRequired)
            .MaximumLength(RoleFieldLimits.NameMaxLength).WithMessage($"Rol adı en fazla {RoleFieldLimits.NameMaxLength} karakter olabilir.").WithErrorCode(RoleErrorCodes.NameTooLong);

        RoleFieldRules.DisplayName(RuleFor(x => x.DisplayName));
        RoleFieldRules.Description(RuleFor(x => x.Description));
    }
}

/// <summary>WP-ROLES-CLOSE-01 — PUT api/roles/{id} had no validator: a blank or unbounded display name went straight in.</summary>
public sealed class UpdateRoleCommandValidator : AbstractValidator<UpdateRoleCommand>
{
    public UpdateRoleCommandValidator()
    {
        RoleFieldRules.DisplayName(RuleFor(x => x.DisplayName));
        RoleFieldRules.Description(RuleFor(x => x.Description));
    }
}

/// <summary>The rules create and update share — written once.</summary>
internal static class RoleFieldRules
{
    /// <summary>Not null, not empty, not only white space.</summary>
    public static bool HasText(string? value) => !string.IsNullOrWhiteSpace(value);

    public static void DisplayName<T>(IRuleBuilderInitial<T, string> rule) => rule
        .Must(HasText).WithMessage("Görünen ad boş bırakılamaz.").WithErrorCode(RoleErrorCodes.DisplayNameRequired)
        .MaximumLength(RoleFieldLimits.DisplayNameMaxLength).WithMessage($"Görünen ad en fazla {RoleFieldLimits.DisplayNameMaxLength} karakter olabilir.").WithErrorCode(RoleErrorCodes.DisplayNameTooLong);

    public static void Description<T>(IRuleBuilderInitial<T, string?> rule) => rule
        .MaximumLength(RoleFieldLimits.DescriptionMaxLength).WithMessage($"Açıklama en fazla {RoleFieldLimits.DescriptionMaxLength} karakter olabilir.").WithErrorCode(RoleErrorCodes.DescriptionTooLong);
}
