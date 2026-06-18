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
}

// AuthService CreateUserRequest: { email, firstName, lastName }. Password is intentionally omitted —
// the backend treats a missing password as an INVITATION and emails a set-password link.
public sealed class UserCreatePayload
{
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
}

// AuthService UpdateUserRequest: { firstName, lastName, isActive } (email immutable).
public sealed class UserUpdatePayload
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
