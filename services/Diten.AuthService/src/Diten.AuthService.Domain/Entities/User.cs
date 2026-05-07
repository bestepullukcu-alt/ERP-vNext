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
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public bool EmailConfirmed { get; private set; }
    public int FailedLoginAttempts { get; private set; }
    public DateTime? LockoutEnd { get; private set; }
    public DateTime? LastLoginAt { get; private set; }

    public void UpdateProfile(string firstName, string lastName)
    {
        FirstName = firstName;
        LastName = lastName;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
    public void ConfirmEmail() => EmailConfirmed = true;
    
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
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
