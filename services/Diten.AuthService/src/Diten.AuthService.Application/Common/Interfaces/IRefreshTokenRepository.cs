using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Common.Interfaces;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct);
    Task CreateAsync(RefreshToken refreshToken, CancellationToken ct);
    Task UpdateAsync(RefreshToken refreshToken, CancellationToken ct);
    Task RevokeAsync(string token, CancellationToken ct);
    /// <summary>Revokes every live refresh token of the user in the tenant; returns how many were revoked (BL-529 audit).</summary>
    Task<long> RevokeAllByUserAsync(Guid userId, Guid tenantId, CancellationToken ct);

    /// <summary>
    /// BL-529 — ends every session of the user in the tenant that could still be used: not revoked, not expired. Each is
    /// stamped with <paramref name="reason"/>; an already revoked token keeps its own time and reason, an expired one is
    /// left alone (it refreshes nothing). Returns how many sessions were ended — the number the audit row reports.
    /// </summary>
    Task<long> RevokeLiveSessionsAsync(Guid userId, Guid tenantId, string reason, CancellationToken ct);
}
