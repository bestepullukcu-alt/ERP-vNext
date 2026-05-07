namespace Diten.Web.Services.Auth;

public interface IAuthGateway
{
    Task<AuthBridgeResult> LoginTenantAsync(string email, string password, Guid tenantId, CancellationToken ct = default);
    Task<AuthBridgeResult> VerifyTenantMfaAsync(string challengeId, string code, CancellationToken ct = default);
    Task<AuthBridgeResult> ResendTenantMfaAsync(string challengeId, CancellationToken ct = default);
    Task<AuthBridgeResult> LoginPlatformAsync(string email, string password, CancellationToken ct = default);
    Task<AuthBridgeResult> RefreshAsync(string accessToken, string refreshToken, Guid? tenantId, CancellationToken ct = default);
    Task LogoutAsync(string accessToken, string refreshToken, Guid? tenantId, CancellationToken ct = default);
}
