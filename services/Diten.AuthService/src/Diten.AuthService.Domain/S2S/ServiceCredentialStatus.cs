namespace Diten.AuthService.Domain.S2S;

public enum ServiceCredentialStatus
{
    Pending = 0,
    Active = 1,
    Previous = 2,
    Revoked = 3,
    Retired = 4
}
