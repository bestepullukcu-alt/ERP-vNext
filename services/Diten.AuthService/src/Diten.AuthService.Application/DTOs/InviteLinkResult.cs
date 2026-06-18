namespace Diten.AuthService.Application.DTOs;

// Result of resend-invite / admin-reset. SetupUrl carries the set-password link ONLY in Development
// (so a dev without SMTP can copy it); it is always null in non-Development environments.
public sealed record InviteLinkResult(string? SetupUrl);
