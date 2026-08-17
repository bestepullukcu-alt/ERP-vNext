namespace Diten.Platform.Domain.Entities;

public sealed record InitialAdminInfo(
    string FirstName,
    string LastName,
    string Email,
    string? Phone = null,
    bool MfaRequired = false,
    bool EmailVerificationRequired = true,
    bool SendInvitationEmail = true);
