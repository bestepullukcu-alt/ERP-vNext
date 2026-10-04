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
}
