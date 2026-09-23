using Diten.AuthService.Domain.Enums;

namespace Diten.AuthService.Domain.Entities;

public sealed class User : EntityBase
{
    private User() { } // EF/Mongo support

    public User(string email, string passwordHash, string firstName, string lastName, Guid tenantId)
    {
        Email = email;
        PasswordHash = passwordHash;
        FirstName = firstName;
        LastName = lastName;
        TenantId = tenantId;
        IsActive = true;
        EmailConfirmed = false;
        FailedLoginAttempts = 0;
        // WP-INFRA-AUTH-ACCOUNT-KIND-01 — a new account is unclassified by construction. Documents that predate
        // the field also read as Unknown (enum default 0) and are never rewritten for it.
        AccountKind = AccountKind.Unknown;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public string Email { get; private set; } = string.Empty;
    public string UserName { get; private set; } = string.Empty;
    public string NormalizedUserName { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public bool EmailConfirmed { get; private set; }
    public int FailedLoginAttempts { get; private set; }
    public DateTime? LockoutEnd { get; private set; }
    public DateTime? LastLoginAt { get; private set; }
    public bool MustChangePassword { get; private set; }
    public DateTime? TemporaryPasswordExpiresAt { get; private set; }
    public DateTime? PasswordChangedAt { get; private set; }
    public string? PasswordResetTokenHash { get; private set; }
    public DateTime? PasswordResetTokenExpiresAt { get; private set; }
    public DateTime? PasswordResetRequestedAt { get; private set; }
    public string? PlatformActorType { get; private set; }

    /// <summary>
    /// WP-INFRA-AUTH-ACCOUNT-KIND-01 — the account-kind FACT (Unknown/Human/Service). Stored as its number; missing
    /// on older documents → <see cref="AccountKind.Unknown"/>. Mutated only through <see cref="SetAccountKind"/>.
    /// </summary>
    public AccountKind AccountKind { get; private set; }

    /// <summary>
    /// Classifies the account. The ONLY write path; callers are the explicit-grant-only SetAccountKind handler and a
    /// permitted explicit kind on create — never an automatic creation path (AccountKindCreationPathsGuardTests).
    /// </summary>
    public void SetAccountKind(AccountKind kind)
    {
        AccountKind = kind;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateProfile(string firstName, string lastName)
    {
        FirstName = firstName;
        LastName = lastName;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetUserName(string userName)
    {
        var normalized = (userName ?? string.Empty).Trim();
        UserName = normalized;
        NormalizedUserName = normalized.ToLowerInvariant();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// WP-AUTH-INVITED-LIFECYCLE-01 — the account was INVITED and its owner has never set a password: it holds only
    /// the unusable placeholder hash, so it cannot sign in whatever <see cref="IsActive"/> says. Derived, never stored.
    /// Each fact rules out one look-alike: <see cref="MustChangePassword"/> (not a normal account),
    /// <see cref="EmailConfirmed"/> false (redeeming the set-password link confirms it; an admin reset of a redeemed
    /// account and a provisioned admin with a temporary password are both confirmed), <see cref="LastLoginAt"/> null
    /// (the account has never been used). A method, not a property, so the Mongo class map never persists it.
    /// </summary>
    public bool IsInvitationPending() => MustChangePassword && !EmailConfirmed && LastLoginAt is null;

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
    public void ConfirmEmail() => EmailConfirmed = true;
    public void SetPlatformActorType(string actorType)
    {
        PlatformActorType = string.IsNullOrWhiteSpace(actorType) ? null : actorType.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void RequirePasswordChange(DateTime? temporaryPasswordExpiresAt)
    {
        MustChangePassword = true;
        TemporaryPasswordExpiresAt = temporaryPasswordExpiresAt;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void ClearPasswordChangeRequirement()
    {
        MustChangePassword = false;
        TemporaryPasswordExpiresAt = null;
        PasswordResetTokenHash = null;
        PasswordResetTokenExpiresAt = null;
        PasswordChangedAt = DateTime.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetPasswordResetToken(string tokenHash, DateTime expiresAtUtc)
    {
        PasswordResetTokenHash = tokenHash;
        PasswordResetTokenExpiresAt = expiresAtUtc;
        PasswordResetRequestedAt = DateTime.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
    
    public void RecordLoginSuccess()
    {
        LastLoginAt = DateTime.UtcNow;
        FailedLoginAttempts = 0;
        LockoutEnd = null;
    }

    public void RecordLoginFailure()
    {
        RecordLoginFailure(5, 15);
    }

    public void RecordLoginFailure(int maxFailedAttempts, int lockoutDurationMinutes)
    {
        FailedLoginAttempts++;
        if (FailedLoginAttempts >= maxFailedAttempts)
        {
            LockoutEnd = DateTime.UtcNow.AddMinutes(lockoutDurationMinutes);
        }
    }

    public void UpdatePassword(string newHashedPassword)
    {
        PasswordHash = newHashedPassword;
        PasswordChangedAt = DateTime.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
