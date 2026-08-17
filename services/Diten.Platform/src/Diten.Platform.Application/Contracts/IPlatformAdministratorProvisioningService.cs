namespace Diten.Platform.Application.Contracts;

public interface IPlatformAdministratorProvisioningService
{
    Task<PlatformAdministratorProvisioningResult> ProvisionAsync(PlatformAdministratorProvisioningRequest request, CancellationToken ct);
    Task SyncAsync(PlatformAdministratorProvisioningSyncRequest request, CancellationToken ct);
}

public sealed record PlatformAdministratorProvisioningRequest(
    string Email,
    string UserName,
    string DisplayName,
    string ActorType,
    IReadOnlyList<string> Roles,
    bool RequirePasswordChange);

public sealed record PlatformAdministratorProvisioningSyncRequest(
    string Email,
    string UserName,
    string DisplayName,
    string ActorType,
    IReadOnlyList<string> Roles);

public sealed record PlatformAdministratorProvisioningResult(
    string? SetupUrl,
    bool EmailSent);
