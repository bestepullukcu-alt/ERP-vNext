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

/// <param name="PreviousEmail">BL-521 FIX3 item 2(a) — the address Auth knows the account by (the record's address before
/// this change); Auth finds the account by it and moves it to <paramref name="Email"/>. Null: found by Email.</param>
public sealed record PlatformAdministratorProvisioningSyncRequest(
    string Email,
    string UserName,
    string DisplayName,
    string ActorType,
    IReadOnlyList<string> Roles,
    string? PreviousEmail = null);

public sealed record PlatformAdministratorProvisioningResult(
    string? SetupUrl,
    bool EmailSent);
