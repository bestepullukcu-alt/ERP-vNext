namespace Diten.Web.Services.Auth;

public interface IAuthGateway
{
    Task<AuthBridgeResult> LoginTenantAsync(string email, string password, Guid tenantId, bool rememberMe = false, CancellationToken ct = default);
    Task<AuthBridgeResult> VerifyTenantMfaAsync(string challengeId, string code, CancellationToken ct = default);
    Task<AuthBridgeResult> ResendTenantMfaAsync(string challengeId, CancellationToken ct = default);
    Task<AuthBridgeResult> LoginPlatformAsync(string email, string password, bool rememberMe = false, CancellationToken ct = default);
    Task<AuthBridgeResult> ChangePlatformPasswordAsync(string currentPassword, string newPassword, bool rememberMe = false, CancellationToken ct = default);
    // FIX-TENANT-MUSTCHANGEPW — forced first-login change for tenant_user.
    Task<AuthBridgeResult> ChangeTenantPasswordAsync(string currentPassword, string newPassword, bool rememberMe = false, CancellationToken ct = default);
    Task<bool> ForgotPlatformPasswordAsync(string email, CancellationToken ct = default);
    Task<AuthBridgeResult> ResetPlatformPasswordAsync(string email, string token, string newPassword, CancellationToken ct = default);
    // Tenant invitation redemption → anonymous AuthService /api/users/set-password (no tenant header/bearer).
    Task<AuthBridgeResult> ResetTenantPasswordAsync(string email, string token, string newPassword, CancellationToken ct = default);
    Task<AuthBridgeResult> RefreshAsync(string accessToken, string refreshToken, Guid? tenantId, CancellationToken ct = default);
    Task LogoutAsync(string accessToken, string refreshToken, Guid? tenantId, CancellationToken ct = default);
}
