namespace Diten.AuthService.Application.DTOs;

public sealed record UserDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    bool IsActive,
    IEnumerable<string> Roles,
    Guid? TenantId,
    // ── Security metrics (Admin panel) ──
    DateTime? LastLoginAt = null,
    int FailedLoginAttempts = 0,
    bool MustChangePassword = false,
    // MFA is governed by tenant login policy, NOT tracked per-user on the User aggregate; handlers
    // surface the literal "TenantPolicy" so the UI can render a policy-level badge.
    string? MfaStatus = null,
    // Development-only: populated by the invitation create path so a dev without SMTP can grab the
    // set-password link from the API response. ALWAYS null in non-Development environments.
    string? SetupUrl = null
);
