using System.Security.Cryptography;
using System.Text;
using Diten.AuthService.Domain.S2S;

namespace Diten.AuthService.Application.S2S;

public enum ExplicitRoleGrantMutationV1 { Grant = 1, Revoke = 2 }

/// <summary>Application-internal foundation model; not an HTTP/producer contract.</summary>
public sealed record ExplicitRoleGrantProvisioningV1(
    Guid TenantId,
    Guid AuthenticatedActorId,
    Guid RoleId,
    Guid PermissionId,
    ExplicitRoleGrantMutationV1 Mutation,
    string IdempotencyKey,
    string TrustedAuthorizationProvenance,
    string CanonicalPayloadHash)
{
    public const int CanonicalFormVersion = 1;

    public static ExplicitRoleGrantProvisioningV1 Create(Guid tenantId, Guid actorId, Guid roleId, Guid permissionId,
        ExplicitRoleGrantMutationV1 mutation, string idempotencyKey, string provenance)
    {
        if (tenantId == Guid.Empty) throw new S2SContractException("Tenant identity is required.", nameof(tenantId));
        if (actorId == Guid.Empty) throw new S2SContractException("Authenticated actor identity is required.", nameof(actorId));
        if (roleId == Guid.Empty) throw new S2SContractException("Role identity is required.", nameof(roleId));
        if (permissionId == Guid.Empty) throw new S2SContractException("Permission identity is required.", nameof(permissionId));
        if (!Enum.IsDefined(mutation)) throw new S2SContractException("Unsupported mutation.", nameof(mutation));
        S2SExactValue.Required(idempotencyKey, nameof(idempotencyKey));
        S2SExactValue.Required(provenance, nameof(provenance));
        if (idempotencyKey.Length > 200) throw new S2SContractException("Idempotency key is too long.", nameof(idempotencyKey));
        var request = new ExplicitRoleGrantProvisioningV1(tenantId, actorId, roleId, permissionId, mutation,
            idempotencyKey, provenance, string.Empty);
        return request with { CanonicalPayloadHash = ComputeHash(request) };
    }

    public static string ComputeHash(ExplicitRoleGrantProvisioningV1 value)
    {
        var fields = new[]
        {
            "diten.auth.explicit-role-grant", CanonicalFormVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
            value.RoleId.ToString("D"), value.PermissionId.ToString("D"), value.Mutation.ToString()
        };
        var canonical = string.Concat(fields.Select(x => $"{Encoding.UTF8.GetByteCount(x)}:{x}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    public void Validate()
    {
        _ = Create(TenantId, AuthenticatedActorId, RoleId, PermissionId, Mutation, IdempotencyKey, TrustedAuthorizationProvenance);
        if (!string.Equals(CanonicalPayloadHash, ComputeHash(this), StringComparison.Ordinal))
            throw new S2SContractException("Canonical payload hash mismatch.", nameof(CanonicalPayloadHash));
    }
}

public enum ExplicitRoleGrantProvisioningStatus
{
    Applied,
    NoOp,
    Unauthorized,
    Forbidden,
    NotFound,
    Conflict,
    Unavailable
}

public sealed record ExplicitRoleGrantProvisioningResult(
    ExplicitRoleGrantProvisioningStatus Status,
    Guid ReceiptId,
    bool AuthorizationStateChanged,
    long AuthorizationVersion,
    string PayloadHash)
{
    public int SuggestedHttpStatusCode => Status switch
    {
        ExplicitRoleGrantProvisioningStatus.Applied => 200,
        ExplicitRoleGrantProvisioningStatus.NoOp => 200,
        ExplicitRoleGrantProvisioningStatus.Unauthorized => 401,
        ExplicitRoleGrantProvisioningStatus.Forbidden => 403,
        ExplicitRoleGrantProvisioningStatus.NotFound => 404,
        ExplicitRoleGrantProvisioningStatus.Conflict => 409,
        _ => 503
    };
}
