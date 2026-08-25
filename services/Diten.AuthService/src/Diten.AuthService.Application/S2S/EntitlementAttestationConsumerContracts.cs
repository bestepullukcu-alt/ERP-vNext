using System.Security.Cryptography;

namespace Diten.AuthService.Application.S2S;

public enum EntitlementAttestationDecisionV1 { Active, Disabled, Expired, Missing, NotApplicable }
public enum EntitlementAttestationProviderFailureV1 { Disabled, Unavailable, Timeout, Malformed, Indeterminate }
public enum EntitlementAttestationOutcomeKind { Continue, Unauthorized, Forbidden, Conflict, ServiceUnavailable }

public readonly record struct EntitlementStateVersionV1(ulong PhysicalEntitlementVersion, ulong SubscriptionVersion, ulong ModuleApplicabilityVersion)
{
    public bool IsComplete => PhysicalEntitlementVersion > 0 && SubscriptionVersion > 0 && ModuleApplicabilityVersion > 0;
}

public sealed record EntitlementAttestationRequestV1(Guid TenantId, string ModuleCode, string RequestHash);
public abstract record EntitlementAttestationProviderResultV1
{
    private EntitlementAttestationProviderResultV1() { }
    public sealed record Attested(string Token) : EntitlementAttestationProviderResultV1;
    public sealed record Failed(EntitlementAttestationProviderFailureV1 Failure) : EntitlementAttestationProviderResultV1;
}

public interface IPlatformEntitlementAttestationProvider
{
    Task<EntitlementAttestationProviderResultV1> GetAsync(EntitlementAttestationRequestV1 request, CancellationToken cancellationToken);
}

public sealed record EntitlementAttestationTrustedKey(string Issuer, string Kid, RSA PublicKey, bool Active, bool IsTestOnly);
public enum EntitlementAttestationKeyResolutionKind { Resolved, Unknown, Unavailable, Indeterminate }
public sealed record EntitlementAttestationKeyResolution(EntitlementAttestationKeyResolutionKind Kind, EntitlementAttestationTrustedKey? Key = null);
public interface IEntitlementAttestationTrustedKeyProvider
{
    Task<EntitlementAttestationKeyResolution> ResolveAsync(string issuer, string kid, CancellationToken cancellationToken);
}

public enum EntitlementVersionFenceResult { Accepted, Older, Incomparable, AuthorityUnavailable }
public interface IEntitlementStateVersionFence
{
    Task<EntitlementVersionFenceResult> ObserveAsync(Guid tenantId, string moduleCode, EntitlementStateVersionV1 version, CancellationToken cancellationToken);
}

public sealed record EntitlementAttestationValidationResult(
    EntitlementAttestationOutcomeKind Kind, string Code, EntitlementAttestationDecisionV1? Decision = null,
    EntitlementStateVersionV1? Version = null, DateTimeOffset? ValidUntilUtc = null)
{
    public static EntitlementAttestationValidationResult Continue(EntitlementAttestationDecisionV1 d, EntitlementStateVersionV1 v, DateTimeOffset until) => new(EntitlementAttestationOutcomeKind.Continue, "entitlement_attestation_active", d, v, until);
    public static EntitlementAttestationValidationResult Unauthorized(string code) => new(EntitlementAttestationOutcomeKind.Unauthorized, code);
    public static EntitlementAttestationValidationResult Forbidden(string code, EntitlementAttestationDecisionV1 d, EntitlementStateVersionV1 v, DateTimeOffset until) => new(EntitlementAttestationOutcomeKind.Forbidden, code, d, v, until);
    public static EntitlementAttestationValidationResult Conflict(string code) => new(EntitlementAttestationOutcomeKind.Conflict, code);
    public static EntitlementAttestationValidationResult Unavailable(string code) => new(EntitlementAttestationOutcomeKind.ServiceUnavailable, code);
}

public sealed record Fu16LocalAuthorizationSnapshot(
    Guid ServicePrincipalId, long PrincipalVersion, long CredentialGeneration, Guid DelegatedActorId,
    long MembershipVersion, Guid ExplicitGrantId, long GrantVersion, long AuthorizationVersion,
    string ReplayJti, string ReplayNonce);

public enum Fu16LocalAuthorizationResultKind { Accepted, StaleOrConcurrent, Unauthorized, Forbidden, AuthorityUnavailable }
public sealed record Fu16LocalAuthorizationResult(Fu16LocalAuthorizationResultKind Kind);

// Closed, activation-independent seam. Implementations must use one Mongo client/session and one snapshot transaction.
public interface IFu16AuthorizationTransactionSession
{
    Task<Fu16LocalAuthorizationResult> ValidateAndConsumeAsync(Fu16LocalAuthorizationSnapshot snapshot, CancellationToken cancellationToken);
}

public static class EntitlementAttestationContractV1
{
    public const string ContractId = "platform.entitlement-attestation";
    public const string ContractVersion = "1.0";
    public const string Issuer = "diten-platform-service";
    public const string Audience = "diten-auth-service";
    public const string Type = "diten-entitlement-attestation+jwt";
    public const string Algorithm = "RS256";
    public static readonly TimeSpan MaximumValidity = TimeSpan.FromSeconds(15);
}
