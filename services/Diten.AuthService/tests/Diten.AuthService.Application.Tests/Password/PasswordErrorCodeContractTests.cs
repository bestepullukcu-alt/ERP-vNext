using System.Xml.Linq;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Common.Services;
using Diten.AuthService.Application.Features.Auth.Commands;
using Diten.AuthService.Application.Features.Auth.Validators;
using Diten.AuthService.Application.Features.Users.Commands;
using Diten.AuthService.Application.Features.Users.Validators;
using FluentValidation;
using FluentValidation.Results;

namespace Diten.AuthService.Application.Tests.Password;

// Guards the error-code bridge for password validation: the AuthService emits stable, machine-readable codes
// (never localized text); the MVC frontend resolves them to the request culture. These tests verify each source
// emits the right code + params, and that every code has a frontend mapping + a resx key in ALL 7 languages.
public sealed class PasswordErrorCodeContractTests
{
    // The single source of truth used by the frontend AuthGateway.ErrorCodeResourceKeys map (kept in lock-step here).
    private static readonly IReadOnlyDictionary<string, string> ExpectedCodeToResourceKey = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [PasswordErrorCodes.Required] = "Password.Error.Required",
        [PasswordErrorCodes.TooShort] = "Password.Error.TooShort",
        [PasswordErrorCodes.TooLong] = "Password.Error.TooLong",
        [PasswordErrorCodes.NeedsUppercase] = "Password.Error.NeedsUppercase",
        [PasswordErrorCodes.NeedsSpecial] = "Password.Error.NeedsSpecial",
        [PasswordErrorCodes.CurrentRequired] = "Password.Error.CurrentRequired",
        [PasswordErrorCodes.NewRequired] = "Password.Error.NewRequired",
        [PasswordErrorCodes.ResetEmailRequired] = "Password.Error.ResetEmailRequired",
        [PasswordErrorCodes.ResetTokenRequired] = "Password.Error.ResetTokenRequired",
    };

    private static readonly string[] SupportedLanguages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    // ── PasswordPolicyService ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PasswordPolicy_empty_password_emits_required_code()
    {
        var failures = await PolicyFailuresAsync("   ", Snapshot());

        var failure = Assert.Single(failures);
        Assert.Equal(PasswordErrorCodes.Required, failure.ErrorCode);
    }

    [Fact]
    public async Task PasswordPolicy_short_password_emits_too_short_code_with_min_length_param()
    {
        var failures = await PolicyFailuresAsync("aA1!x", Snapshot(minLength: 10));

        var tooShort = Assert.Single(failures, f => f.ErrorCode == PasswordErrorCodes.TooShort);
        var @params = Assert.IsAssignableFrom<IReadOnlyDictionary<string, string>>(tooShort.CustomState);
        Assert.Equal("10", @params["minLength"]);
    }

    [Fact]
    public async Task PasswordPolicy_missing_uppercase_and_special_emit_their_codes()
    {
        // long enough, lowercase+digits only → uppercase + special failures, no too_short.
        var failures = await PolicyFailuresAsync("abcdefghij123", Snapshot(minLength: 10));

        Assert.Contains(failures, f => f.ErrorCode == PasswordErrorCodes.NeedsUppercase);
        Assert.Contains(failures, f => f.ErrorCode == PasswordErrorCodes.NeedsSpecial);
        Assert.DoesNotContain(failures, f => f.ErrorCode == PasswordErrorCodes.TooShort);
    }

    [Fact]
    public async Task PasswordPolicy_never_emits_a_non_password_code()
    {
        var failures = await PolicyFailuresAsync("x", Snapshot(minLength: 10));

        Assert.All(failures, f => Assert.StartsWith(PasswordErrorCodes.Prefix, f.ErrorCode));
    }

    // ── ChangePasswordCommandValidator ─────────────────────────────────────────────────────────────

    [Fact]
    public void ChangePassword_empty_current_and_new_emit_required_codes()
    {
        var result = new ChangePasswordCommandValidator().Validate(new ChangePasswordCommand("user", "", ""));

        Assert.Contains(result.Errors, f => f.ErrorCode == PasswordErrorCodes.CurrentRequired);
        Assert.Contains(result.Errors, f => f.ErrorCode == PasswordErrorCodes.NewRequired);
        Assert.All(result.Errors, f => Assert.False(HasTurkish(f.ErrorMessage), $"leaked non-English: {f.ErrorMessage}"));
    }

    [Fact]
    public void ChangePassword_too_long_new_emits_too_long_code_with_max_length_param()
    {
        var result = new ChangePasswordCommandValidator()
            .Validate(new ChangePasswordCommand("user", "current", new string('a', 129)));

        var tooLong = Assert.Single(result.Errors, f => f.ErrorCode == PasswordErrorCodes.TooLong);
        var @params = Assert.IsAssignableFrom<IReadOnlyDictionary<string, string>>(tooLong.CustomState);
        Assert.Equal("128", @params["maxLength"]);
    }

    // ── SetTenantPasswordCommandValidator ──────────────────────────────────────────────────────────

    [Fact]
    public void SetTenantPassword_empty_fields_emit_reset_codes()
    {
        var result = new SetTenantPasswordCommandValidator().Validate(new SetTenantPasswordCommand("", "", ""));

        Assert.Contains(result.Errors, f => f.ErrorCode == PasswordErrorCodes.ResetEmailRequired);
        Assert.Contains(result.Errors, f => f.ErrorCode == PasswordErrorCodes.ResetTokenRequired);
        Assert.Contains(result.Errors, f => f.ErrorCode == PasswordErrorCodes.NewRequired);
    }

    [Fact]
    public void SetTenantPassword_emits_no_turkish_message()
    {
        var result = new SetTenantPasswordCommandValidator()
            .Validate(new SetTenantPasswordCommand("not-an-email", "", new string('a', 200)));

        Assert.All(result.Errors, f => Assert.False(HasTurkish(f.ErrorMessage), $"leaked non-English: {f.ErrorMessage}"));
    }

    // ── Contract guards (code ⇔ frontend map ⇔ resx in all 7 languages) ─────────────────────────────

    [Fact]
    public void Every_password_code_constant_has_exactly_one_resource_key_mapping()
    {
        var declaredCodes = DeclaredPasswordCodes();

        Assert.Equal(declaredCodes.OrderBy(c => c), ExpectedCodeToResourceKey.Keys.OrderBy(c => c));
        // no orphan resx key mapping without a backing constant, and vice-versa (set equality above covers both).
        Assert.Equal(ExpectedCodeToResourceKey.Count, ExpectedCodeToResourceKey.Values.Distinct().Count());
    }

    [Fact]
    public void Frontend_AuthGateway_maps_every_code_and_key()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "frontend", "Diten.Web", "Services", "Auth", "AuthGateway.cs"));

        foreach (var (code, key) in ExpectedCodeToResourceKey)
        {
            Assert.True(source.Contains($"\"{code}\"", StringComparison.Ordinal), $"AuthGateway is missing code mapping: {code}");
            Assert.True(source.Contains($"\"{key}\"", StringComparison.Ordinal), $"AuthGateway is missing resource key: {key}");
        }
    }

    [Fact]
    public void Every_resource_key_exists_in_all_seven_resx_files()
    {
        var resourcesDir = Path.Combine(RepoRoot(), "frontend", "Diten.Web", "Resources");

        foreach (var language in SupportedLanguages)
        {
            var keys = ResxKeys(Path.Combine(resourcesDir, $"SharedResource.{language}.resx"));
            foreach (var key in ExpectedCodeToResourceKey.Values)
            {
                Assert.True(keys.Contains(key), $"SharedResource.{language}.resx is missing key: {key}");
            }
        }
    }

    [Fact]
    public void Parameterized_resource_values_keep_the_zero_placeholder_in_all_languages()
    {
        var resourcesDir = Path.Combine(RepoRoot(), "frontend", "Diten.Web", "Resources");
        string[] parameterized = ["Password.Error.TooShort", "Password.Error.TooLong"];

        foreach (var language in SupportedLanguages)
        {
            var values = ResxValues(Path.Combine(resourcesDir, $"SharedResource.{language}.resx"));
            foreach (var key in parameterized)
            {
                Assert.True(values[key].Contains("{0}", StringComparison.Ordinal),
                    $"SharedResource.{language}.resx value for {key} dropped the {{0}} placeholder.");
            }
        }
    }

    // ── helpers ────────────────────────────────────────────────────────────────────────────────────

    private static async Task<IReadOnlyList<ValidationFailure>> PolicyFailuresAsync(string password, TenantLoginSettingsSnapshot snapshot)
    {
        var service = new PasswordPolicyService(new FakeSettingsClient(snapshot), new NoopAuditService());
        try
        {
            await service.ValidateTenantPasswordAsync(snapshot.TenantId, null, password, "test", CancellationToken.None);
            return [];
        }
        catch (ValidationException ex)
        {
            return ex.Errors.ToList();
        }
    }

    private static TenantLoginSettingsSnapshot Snapshot(int minLength = 10) => new(
        TenantId: Guid.NewGuid(),
        TwoFactorEnabled: false,
        MfaRequired: false,
        EmailLoginEnabled: true,
        PhoneLoginEnabled: false,
        PasswordMinLength: minLength,
        PasswordRequireUppercase: true,
        PasswordRequireLowercase: true,
        PasswordRequireDigit: true,
        PasswordRequireSpecialChar: true,
        PasswordExpirationDays: null,
        SessionTimeoutMinutes: 30,
        RefreshTokenLifetimeDays: 7,
        MaxFailedLoginAttempts: 5,
        LockoutDurationMinutes: 15);

    private static IReadOnlyCollection<string> DeclaredPasswordCodes() =>
        typeof(PasswordErrorCodes)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f is { IsLiteral: true, IsInitOnly: false } && f.Name != nameof(PasswordErrorCodes.Prefix))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

    private static HashSet<string> ResxKeys(string path) =>
        XDocument.Load(path).Root!.Elements("data")
            .Select(d => (string?)d.Attribute("name"))
            .Where(n => n is not null)
            .Select(n => n!)
            .ToHashSet(StringComparer.Ordinal);

    private static Dictionary<string, string> ResxValues(string path) =>
        XDocument.Load(path).Root!.Elements("data")
            .Where(d => d.Attribute("name") is not null)
            .ToDictionary(d => (string)d.Attribute("name")!, d => d.Element("value")?.Value ?? string.Empty, StringComparer.Ordinal);

    private static bool HasTurkish(string? text) =>
        text is not null && text.Any(c => "çğıöşüÇĞİÖŞÜ".Contains(c));

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Resources")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repo root (frontend/Diten.Web/Resources) from the test output directory.");
    }

    private sealed class FakeSettingsClient(TenantLoginSettingsSnapshot snapshot) : ITenantLoginSettingsClient
    {
        public Task<TenantLoginSettingsSnapshot> GetAsync(Guid tenantId, CancellationToken ct) => Task.FromResult(snapshot);
    }

    private sealed class NoopAuditService : IAuthAuditService
    {
        public Task WriteEmptyRoleLoginAsync(Guid userId, Guid tenantId, string email, CancellationToken ct = default) => Task.CompletedTask;
        public Task WriteAsync(string eventName, Guid? userId, Guid tenantId, string metadata, CancellationToken ct = default) => Task.CompletedTask;
    }
}
