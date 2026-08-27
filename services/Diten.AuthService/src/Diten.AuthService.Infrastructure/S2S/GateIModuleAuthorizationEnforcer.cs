using Diten.AuthService.Application.S2S;

namespace Diten.AuthService.Infrastructure.S2S;

/// <summary>
/// Default-off composition seam for exact Gate-I owner/operation/permission enforcement.
/// It does not register an endpoint, authentication scheme, key, credential or hosted service.
/// </summary>
public sealed class GateIModuleAuthorizationEnforcer(EntitlementAttestationConsumer entitlementConsumer)
{
    public Task<EntitlementAttestationValidationResult> EnforceAsync(
        GateIModuleAuthorizationBindingV1 binding,
        Fu16LocalAuthorizationSnapshot localSnapshot,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var manifest = GateIPermissionCatalogManifests.All.SingleOrDefault(x =>
            string.Equals(x.OwnerModuleId, binding.OwnerModuleId, StringComparison.Ordinal));
        if (manifest is null || !ExactManifestBinding(manifest, binding))
            return Task.FromResult(EntitlementAttestationValidationResult.Unauthorized("auth_module_binding_invalid"));

        var pair = manifest.Entries.SingleOrDefault(x =>
            string.Equals(x.OperationId, binding.OperationId, StringComparison.Ordinal));
        if (pair is null || !string.Equals(pair.PermissionKey, binding.PermissionKey, StringComparison.Ordinal))
            return Task.FromResult(EntitlementAttestationValidationResult.Forbidden(
                "auth_operation_permission_denied", EntitlementAttestationDecisionV1.Active, default, DateTimeOffset.MinValue));

        return entitlementConsumer.EnforceAsync(
            new EntitlementAttestationRequestV1(binding.TenantId, binding.ModuleEntitlementCode, binding.RequestHash),
            localSnapshot,
            cancellationToken);
    }

    private static bool ExactManifestBinding(PermissionCatalogManifestV1 manifest, GateIModuleAuthorizationBindingV1 binding) =>
        string.Equals(manifest.ModuleEntitlementCode, binding.ModuleEntitlementCode, StringComparison.Ordinal) &&
        string.Equals(manifest.ServiceIdentity, binding.ServiceIdentity, StringComparison.Ordinal) &&
        string.Equals(manifest.ClientId, binding.ClientId, StringComparison.Ordinal) &&
        string.Equals(manifest.Audience, binding.Audience, StringComparison.Ordinal) &&
        string.Equals(manifest.ProtocolScope, binding.ProtocolScope, StringComparison.Ordinal);
}
