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

/// <param name="Email">The administrator's address — only the key Auth finds the account by. BL-521 FIX4 item 1: an
/// administrator's e-mail never changes, so Auth never moves an account to another address (FIX3's PreviousEmail is gone).</param>
/// <param name="Status">BL-521 FIX4 item 5 — the record's status (its enum name): Auth switches the account on only for
/// Active and otherwise leaves the account's own state alone.</param>
public sealed record PlatformAdministratorProvisioningSyncRequest(
    string Email,
    string UserName,
    string DisplayName,
    string ActorType,
    IReadOnlyList<string> Roles,
    string Status);

public sealed record PlatformAdministratorProvisioningResult(
    string? SetupUrl,
    bool EmailSent);
