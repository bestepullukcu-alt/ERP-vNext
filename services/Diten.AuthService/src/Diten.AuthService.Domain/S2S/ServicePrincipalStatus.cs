namespace Diten.AuthService.Domain.S2S;

public enum ServicePrincipalStatus
{
    Pending = 0,
    Active = 1,
    Suspended = 2,
    Revoked = 3,
    Retired = 4
}
