using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.DTOs;
using MediatR;

namespace Diten.AuthService.Application.Features.Auth.Commands;

/// <summary>
/// FIX-TENANT-MUSTCHANGEPW — forced first-login password change for a tenant_user (mirrors the platform forced
/// change). Identity (UserId/TenantId) comes from the validated JWT via the controller. On success the user's
/// MustChangePassword is cleared and FRESH tokens are issued (RequiresPasswordChange=false) so the shell guard
/// stops redirecting immediately.
/// </summary>
public sealed record ForcedChangeTenantPasswordCommand(
    Guid UserId,
    Guid TenantId,
    string CurrentPassword,
    string NewPassword,
    string RequestIp,
    string? UserAgent,
    bool RememberMe = false
) : IRequest<Response<AuthResponse>>;
