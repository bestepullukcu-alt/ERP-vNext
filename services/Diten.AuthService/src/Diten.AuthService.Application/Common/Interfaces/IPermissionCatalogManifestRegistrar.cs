using Diten.AuthService.Application.S2S;

namespace Diten.AuthService.Application.Common.Interfaces;

public enum PermissionCatalogRegistrationStatus { Registered, NoOp, Conflict, Unavailable }
public sealed record PermissionCatalogRegistrationResult(PermissionCatalogRegistrationStatus Status, Guid RegistrationId, string PayloadHash);

public interface IPermissionCatalogManifestRegistrar
{
    Task<PermissionCatalogRegistrationResult> RegisterAsync(PermissionCatalogManifestV1 manifest, CancellationToken cancellationToken);
}
