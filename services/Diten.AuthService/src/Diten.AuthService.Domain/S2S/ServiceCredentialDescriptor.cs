using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Domain.S2S;

public sealed class ServiceCredentialDescriptor : GlobalEntityBase
{
    public const string RequiredAlgorithm = "RS256";
    public const int MinimumRsaKeySizeBits = 3072;

    private ServiceCredentialDescriptor()
    {
    }

    public ServiceCredentialDescriptor(
        Guid credentialId,
        Guid servicePrincipalId,
        string kid,
        string algorithm,
        int publicKeySizeBits,
        string publicKeyReference,
        string thumbprint,
        DateTimeOffset notBeforeUtc,
        DateTimeOffset expiresAtUtc,
        long generation,
        DateTimeOffset? overlapValidUntilUtc,
        string createdBy)
    {
        if (credentialId == Guid.Empty) throw new S2SContractException("Credential id is required.", nameof(credentialId));
        if (servicePrincipalId == Guid.Empty) throw new S2SContractException("Service principal id is required.", nameof(servicePrincipalId));
        if (!string.Equals(algorithm, RequiredAlgorithm, StringComparison.Ordinal)) throw new S2SContractException("Only exact RS256 metadata is accepted.", nameof(algorithm));
        if (publicKeySizeBits < MinimumRsaKeySizeBits) throw new S2SContractException("RSA key metadata must be at least 3072 bits.", nameof(publicKeySizeBits));
        if (expiresAtUtc <= notBeforeUtc) throw new S2SContractException("Expiry must be after not-before.", nameof(expiresAtUtc));
        if (generation <= 0) throw new S2SContractException("Generation must be positive.", nameof(generation));
        if (overlapValidUntilUtc is not null && (overlapValidUntilUtc <= notBeforeUtc || overlapValidUntilUtc > expiresAtUtc))
            throw new S2SContractException("Overlap cutoff must be inside the credential validity interval.", nameof(overlapValidUntilUtc));

        Id = credentialId;
        CredentialId = credentialId;
        ServicePrincipalId = servicePrincipalId;
        Kid = S2SExactValue.Required(kid, nameof(kid));
        Algorithm = algorithm;
        PublicKeySizeBits = publicKeySizeBits;
        PublicKeyReference = S2SExactValue.Required(publicKeyReference, nameof(publicKeyReference));
        Thumbprint = S2SExactValue.Required(thumbprint, nameof(thumbprint));
        NotBeforeUtc = notBeforeUtc;
        ExpiresAtUtc = expiresAtUtc;
        Generation = generation;
        OverlapValidUntilUtc = overlapValidUntilUtc;
        CreatedBy = S2SExactValue.Required(createdBy, nameof(createdBy));
    }

    public Guid CredentialId { get; private init; }
    public Guid ServicePrincipalId { get; private init; }
    public string Kid { get; private init; } = string.Empty;
    public string Algorithm { get; private init; } = string.Empty;
    public int PublicKeySizeBits { get; private init; }
    public string PublicKeyReference { get; private init; } = string.Empty;
    public string Thumbprint { get; private init; } = string.Empty;
    public DateTimeOffset NotBeforeUtc { get; private init; }
    public DateTimeOffset ExpiresAtUtc { get; private init; }
    public long Generation { get; private init; }
    public DateTimeOffset? OverlapValidUntilUtc { get; private init; }
    public ServiceCredentialStatus Status { get; private set; } = ServiceCredentialStatus.Pending;

    public void TransitionTo(ServiceCredentialStatus target, string actor, DateTimeOffset now)
    {
        if (!CanTransition(Status, target)) throw new S2SContractException($"Transition from {Status} to {target} is forbidden.", nameof(target));
        Status = target;
        UpdatedAt = now;
        UpdatedBy = S2SExactValue.Required(actor, nameof(actor));
    }

    public static bool CanTransition(ServiceCredentialStatus current, ServiceCredentialStatus target) =>
        (current, target) switch
        {
            (ServiceCredentialStatus.Pending, ServiceCredentialStatus.Active) => true,
            (ServiceCredentialStatus.Pending, ServiceCredentialStatus.Revoked) => true,
            (ServiceCredentialStatus.Active, ServiceCredentialStatus.Previous) => true,
            (ServiceCredentialStatus.Active, ServiceCredentialStatus.Revoked) => true,
            (ServiceCredentialStatus.Previous, ServiceCredentialStatus.Revoked) => true,
            (ServiceCredentialStatus.Previous, ServiceCredentialStatus.Retired) => true,
            _ => false
        };
}
