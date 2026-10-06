using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.Governance;

// FE-C 3/3 (MOD-0018-FU9) — tenant Users screen view models. Backed by AuthService /api/users.
public sealed class UserEditViewModel
{
    public Guid? Id { get; set; }

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    // Required on create only (validated in the controller); ignored on edit.
    public string? Password { get; set; }

    [Required]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    public string LastName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}

public sealed class UserDetailViewModel
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public List<string> Roles { get; set; } = [];

    // Security metrics (Admin panel).
    public DateTime? LastLoginAt { get; set; }
    public int FailedLoginAttempts { get; set; }
    public bool MustChangePassword { get; set; }
    public string? MfaStatus { get; set; }

    // WP-AUTH-USER-KIND-UPDATE-01 — the enum NAME AuthService reports; the edit form's select starts from it.
    public string? AccountKind { get; set; }

    // WP-AUTH-INVITED-LIFECYCLE-01, finding 22 (owner, 2026-09-24): AuthService's DERIVED status (Invited · Inactive ·
    // Active). The list row carried it, this detail model dropped it, so the edit form fell back to IsActive and drew
    // the activation switch for an invited account — the exact control the lifecycle work had removed. Only the
    // server can say "Invited"; the form must receive it from here, not guess it.
    public string? Status { get; set; }
}

// AuthService CreateUserRequest: { email, firstName, lastName }. Password is intentionally omitted —
// the backend treats a missing password as an INVITATION and emails a set-password link.
public sealed class UserCreatePayload
{
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
}

// AuthService UpdateUserRequest: { firstName, lastName, isActive, accountKind? } (email immutable).
// AccountKind null ⇒ the kind is not touched; a change needs auth.users.account-kind.manage (403 otherwise).
public sealed class UserUpdatePayload
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string? AccountKind { get; set; }
}
