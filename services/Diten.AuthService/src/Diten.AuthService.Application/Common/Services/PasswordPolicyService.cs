using System.Globalization;
using System.Security.Cryptography;
using Diten.AuthService.Application.Common.Interfaces;
using FluentValidation;
using FluentValidation.Results;

namespace Diten.AuthService.Application.Common.Services;

public sealed class PasswordPolicyService : IPasswordPolicyService
{
    private const string Uppercase = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Lowercase = "abcdefghijkmnopqrstuvwxyz";
    private const string Digits = "23456789";
    private const string Specials = "!@$%*?";

    private readonly ITenantLoginSettingsClient _tenantLoginSettingsClient;
    private readonly IAuthAuditService _authAuditService;

    public PasswordPolicyService(
        ITenantLoginSettingsClient tenantLoginSettingsClient,
        IAuthAuditService authAuditService)
    {
        _tenantLoginSettingsClient = tenantLoginSettingsClient;
        _authAuditService = authAuditService;
    }

    public async Task ValidateTenantPasswordAsync(Guid tenantId, Guid? userId, string password, string context, CancellationToken ct)
    {
        var settings = await _tenantLoginSettingsClient.GetAsync(tenantId, ct);
        var failures = Validate(password, settings).ToList();
        if (failures.Count == 0)
        {
            return;
        }

        await _authAuditService.WriteAsync(
            "tenant_password_policy_violation",
            userId,
            tenantId,
            $"{{\"context\":\"{SanitizeContext(context)}\"}}",
            ct);

        throw new ValidationException(failures);
    }

    public string GenerateTemporaryPassword(TenantLoginSettingsSnapshot settings)
    {
        var requiredChars = new List<char>
        {
            Pick(Lowercase),
            Pick(Digits)
        };

        if (settings.PasswordRequireUppercase)
        {
            requiredChars.Add(Pick(Uppercase));
        }

        if (settings.PasswordRequireSpecialChar)
        {
            requiredChars.Add(Pick(Specials));
        }

        var length = Math.Clamp(settings.PasswordMinLength, 10, 128);
        var all = Uppercase + Lowercase + Digits + Specials;
        while (requiredChars.Count < length)
        {
            requiredChars.Add(Pick(all));
        }

        return new string(requiredChars.OrderBy(_ => RandomNumberGenerator.GetInt32(int.MaxValue)).ToArray());
    }

    private static IEnumerable<ValidationFailure> Validate(string password, TenantLoginSettingsSnapshot settings)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            yield return Failure(PasswordErrorCodes.Required, "Password is required.");
            yield break;
        }

        var minLength = Math.Clamp(settings.PasswordMinLength, 8, 128);
        if (password.Length < minLength)
        {
            yield return Failure(
                PasswordErrorCodes.TooShort,
                $"Password must be at least {minLength} characters.",
                new Dictionary<string, string> { ["minLength"] = minLength.ToString(CultureInfo.InvariantCulture) });
        }

        if (settings.PasswordRequireUppercase && !password.Any(char.IsUpper))
        {
            yield return Failure(PasswordErrorCodes.NeedsUppercase, "Password must contain at least one uppercase letter.");
        }

        if (settings.PasswordRequireSpecialChar && !password.Any(ch => !char.IsLetterOrDigit(ch)))
        {
            yield return Failure(PasswordErrorCodes.NeedsSpecial, "Password must contain at least one special character.");
        }
    }

    // Carry the stable code (ErrorCode) and its params (CustomState) so ExceptionHandlingBehavior can serialize a
    // machine-readable descriptor; ErrorMessage stays as the English fallback for `detail`/logging.
    private static ValidationFailure Failure(string code, string englishFallback, IReadOnlyDictionary<string, string>? @params = null)
        => new("Password", englishFallback)
        {
            ErrorCode = code,
            CustomState = @params
        };

    private static char Pick(string source)
    {
        return source[RandomNumberGenerator.GetInt32(source.Length)];
    }

    private static string SanitizeContext(string context)
    {
        return new string((context ?? string.Empty)
            .Where(ch => char.IsLetterOrDigit(ch) || ch is '_' or '-' or '.')
            .Take(64)
            .ToArray());
    }
}
