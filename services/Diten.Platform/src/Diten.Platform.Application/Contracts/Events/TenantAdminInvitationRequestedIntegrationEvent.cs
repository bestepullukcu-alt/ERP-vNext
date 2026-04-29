namespace Diten.Platform.Application.Contracts.Events;

/// <summary>
/// Raised when a tenant is registered with an initial admin.
/// The AuthService will consume this to create the invitation-based onboarding flow.
/// Dispatch is NOT implemented in the current task — only the contract is defined.
/// </summary>
public sealed record TenantAdminInvitationRequestedIntegrationEvent(
    Guid EventId,
    Guid TenantId,
    string TenantCode,
    string TenantSlug,
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    bool MfaRequired,
    bool EmailVerificationRequired,
    bool SendInvitationEmail,
    DateTimeOffset OccurredAt,
    string Actor);
