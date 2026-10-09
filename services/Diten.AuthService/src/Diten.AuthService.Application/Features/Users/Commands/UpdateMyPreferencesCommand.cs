using Diten.AuthService.Application.Common;
using MediatR;

namespace Diten.AuthService.Application.Features.Users.Commands;

/// <summary>
/// WP-EMAIL-LANG-01 — the signed-in user's own preferences. <paramref name="UserId"/> and <paramref name="TenantId"/> are
/// NOT request data: the controller takes them from the access token (sub, tenant_id) only. Null language clears it.
/// </summary>
public sealed record UpdateMyPreferencesCommand(
    Guid UserId,
    Guid TenantId,
    string? PreferredLanguage,
    string? CorrelationId = null
) : IRequest<Response<MyPreferencesDto>>;

public sealed record MyPreferencesDto(string? PreferredLanguage);
