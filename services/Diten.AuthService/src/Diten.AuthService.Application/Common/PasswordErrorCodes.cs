namespace Diten.AuthService.Application.Common;

// Stable, machine-readable error codes for password validation failures. The AuthService is a headless API with
// no reliable UI culture, so it emits ONLY these codes (never localized text); the MVC frontend resolves each
// code -> a culture-specific SharedResource string. Keep every code in exact 1:1 sync with the frontend code->key
// map and the SharedResource.*.resx keys (all 7 languages). Changing a value here is a breaking contract change.
public static class PasswordErrorCodes
{
    public const string Prefix = "password.";

    public const string Required = "password.required";
    public const string TooShort = "password.too_short";              // param: minLength
    public const string TooLong = "password.too_long";                // param: maxLength
    public const string NeedsUppercase = "password.needs_uppercase";
    public const string NeedsSpecial = "password.needs_special";
    public const string CurrentRequired = "password.current_required";
    public const string NewRequired = "password.new_required";
    public const string ResetEmailRequired = "password.reset_email_required";
    public const string ResetTokenRequired = "password.reset_token_required";
}
