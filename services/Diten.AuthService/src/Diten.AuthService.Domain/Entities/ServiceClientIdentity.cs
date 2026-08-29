namespace Diten.AuthService.Domain.Entities;

public sealed class ServiceClientIdentity : GlobalEntityBase
{
    public string ClientCode { get; init; } = string.Empty;
    public string ServiceName { get; init; } = string.Empty;
    public string? AllowedAudience { get; init; }
    public string ActiveCredentialHash { get; set; } = string.Empty;
    public string ActiveCredentialVersion { get; set; } = string.Empty;
    public string? PreviousCredentialHash { get; set; }
    public string? PreviousCredentialVersion { get; set; }
    public DateTimeOffset? PreviousValidUntilUtc { get; set; }
    public bool IsRevoked { get; set; }
}
