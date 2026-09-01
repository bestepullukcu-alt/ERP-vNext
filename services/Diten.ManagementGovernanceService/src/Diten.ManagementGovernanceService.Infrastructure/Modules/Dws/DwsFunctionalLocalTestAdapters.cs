using Diten.ManagementGovernanceService.Application.Features.Dws;
using Diten.ManagementGovernanceService.Application.Modules.Dws;
using Diten.ManagementGovernanceService.Domain.Modules.Dws;
using Diten.ManagementGovernanceService.Persistence.Modules.Dws;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.ManagementGovernanceService.Infrastructure.Modules.Dws;

public enum DwsLocalContextDisposition
{
    Accepted,
    Malformed,
    NotFound,
    SoftDeleted,
    CrossTenant,
    ActorInvisible,
    StaleFence,
    ConflictingIdentity,
    Unavailable,
    Timeout,
    MalformedAuthority,
    Indeterminate
}

public sealed record DwsLocalMod0117FixtureSnapshot(
    DwsTrustedActorContext Context,
    ExternalContextReference Reference,
    long AuthorityFence,
    DwsLocalContextDisposition Disposition);

public sealed class DwsLocalMod0117Fixture
{
    private readonly object _gate = new();
    private DwsLocalMod0117FixtureSnapshot? _snapshot;

    public void Configure(DwsLocalMod0117FixtureSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        DwsLocalTrustedIdentity.Require(snapshot.Context);
        if (snapshot.AuthorityFence <= 0)
            throw new DwsValidationException(DwsErrors.InvalidContextReference);
        lock (_gate) _snapshot = snapshot;
    }

    public void AdvanceAuthorityFence()
    {
        lock (_gate)
        {
            var current = RequireSnapshot();
            _snapshot = current with { AuthorityFence = checked(current.AuthorityFence + 1) };
        }
    }

    public void SetDisposition(DwsLocalContextDisposition disposition)
    {
        lock (_gate) _snapshot = RequireSnapshot() with { Disposition = disposition };
    }

    public void ReplaceReference(ExternalContextReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        lock (_gate) _snapshot = RequireSnapshot() with { Reference = reference };
    }

    internal DwsLocalMod0117FixtureSnapshot Snapshot()
    {
        lock (_gate) return RequireSnapshot();
    }

    private DwsLocalMod0117FixtureSnapshot RequireSnapshot() =>
        _snapshot ?? throw new DwsValidationException(DwsErrors.ExternalContextAuthorityUnavailable);
}

public sealed class LocalTestMod0117FunctionalContextValidator(DwsLocalMod0117Fixture fixture)
    : IMod0117DwsContextValidator
{
    public Task<DwsMod0117ContextSnapshot> ValidateAsync(
        DwsTrustedActorContext context,
        ExternalContextReference reference,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DwsLocalTrustedIdentity.Require(context);
        ArgumentNullException.ThrowIfNull(reference);

        var snapshot = fixture.Snapshot();
        if (!SameActors(snapshot.Context, context))
            throw new DwsNotFoundException();

        if (reference.ContractName != ExternalContextReference.RequiredContractName
            || reference.ContractVersion != ExternalContextReference.RequiredContractVersion)
            throw new DwsValidationException(DwsErrors.InvalidContextReference);

        if (snapshot.Reference != reference)
            throw new DwsConflictException(DwsErrors.ExternalContextConflict);

        switch (snapshot.Disposition)
        {
            case DwsLocalContextDisposition.Accepted:
                return Task.FromResult(Capture(snapshot));
            case DwsLocalContextDisposition.Malformed:
                throw new DwsValidationException(DwsErrors.InvalidContextReference);
            case DwsLocalContextDisposition.NotFound:
            case DwsLocalContextDisposition.SoftDeleted:
            case DwsLocalContextDisposition.CrossTenant:
            case DwsLocalContextDisposition.ActorInvisible:
                throw new DwsNotFoundException();
            case DwsLocalContextDisposition.StaleFence:
            case DwsLocalContextDisposition.ConflictingIdentity:
                throw new DwsConflictException(DwsErrors.ExternalContextConflict);
            case DwsLocalContextDisposition.Unavailable:
            case DwsLocalContextDisposition.Timeout:
            case DwsLocalContextDisposition.MalformedAuthority:
            case DwsLocalContextDisposition.Indeterminate:
                throw new DwsValidationException(DwsErrors.ExternalContextAuthorityUnavailable);
            default:
                throw new DwsValidationException(DwsErrors.ExternalContextAuthorityUnavailable);
        }
    }

    public Task RevalidateAsync(
        DwsTrustedActorContext context,
        ExternalContextReference reference,
        DwsMod0117ContextSnapshot captured,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DwsLocalTrustedIdentity.Require(context);
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(captured);

        if (captured.TenantId != context.TenantId
            || captured.EffectiveActorId != context.EffectiveActorId
            || captured.DelegatedActorId != context.DelegatedActorId
            || captured.Reference != reference)
            throw new DwsConflictException(DwsErrors.ExternalContextConflict);

        var current = fixture.Snapshot();
        if (current.Disposition is DwsLocalContextDisposition.Unavailable
            or DwsLocalContextDisposition.Timeout
            or DwsLocalContextDisposition.MalformedAuthority
            or DwsLocalContextDisposition.Indeterminate)
            throw new DwsValidationException(DwsErrors.ExternalContextAuthorityUnavailable);
        if (current.Disposition != DwsLocalContextDisposition.Accepted
            || current.AuthorityFence != captured.AuthorityFence
            || current.Reference != captured.Reference
            || !SameActors(current.Context, context))
            throw new DwsConflictException(DwsErrors.ExternalContextConflict);
        return Task.CompletedTask;
    }

    private static DwsMod0117ContextSnapshot Capture(DwsLocalMod0117FixtureSnapshot snapshot) => new(
        snapshot.Context.TenantId,
        snapshot.Context.EffectiveActorId,
        snapshot.Context.DelegatedActorId,
        snapshot.Reference,
        snapshot.AuthorityFence);

    private static bool SameActors(DwsTrustedActorContext expected, DwsTrustedActorContext actual) =>
        expected.TenantId == actual.TenantId
        && expected.EffectiveActorId == actual.EffectiveActorId
        && expected.DelegatedActorId == actual.DelegatedActorId;
}

public enum DwsLocalAuthorizationDisposition
{
    Accepted,
    AuthenticationInvalid,
    EntitlementDenied,
    ExplicitGrantDenied,
    PermissionDenied,
    Unavailable,
    Timeout,
    Malformed,
    Indeterminate,
    StaleFreshness
}

public sealed record DwsLocalFu16FixtureSnapshot(
    string ModuleCode,
    string ModuleEntitlementCode,
    DwsTrustedActorContext Context,
    string Operation,
    string Permission,
    bool HasExplicitTenantGrant,
    long PrincipalGeneration,
    long CredentialGeneration,
    long AuthorizationVersion,
    long EntitlementVersion,
    DwsLocalAuthorizationDisposition Disposition);

public sealed class DwsLocalFu16Fixture
{
    private readonly object _gate = new();
    private DwsLocalFu16FixtureSnapshot? _snapshot;

    public void Configure(DwsLocalFu16FixtureSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        DwsLocalTrustedIdentity.Require(snapshot.Context);
        if (!string.Equals(snapshot.ModuleCode, DwsFunctionalAuthorizationBinding.ModuleCode, StringComparison.Ordinal)
            || !string.Equals(snapshot.ModuleEntitlementCode, DwsFunctionalAuthorizationBinding.ModuleEntitlementCode, StringComparison.Ordinal)
            || snapshot.PrincipalGeneration <= 0
            || snapshot.CredentialGeneration <= 0
            || snapshot.AuthorizationVersion <= 0
            || snapshot.EntitlementVersion <= 0)
            throw new DwsValidationException(DwsErrors.AuthorizationAuthorityUnavailable);
        lock (_gate) _snapshot = snapshot;
    }

    public void AdvancePrincipalGeneration() => Mutate(current => current with { PrincipalGeneration = checked(current.PrincipalGeneration + 1) });
    public void AdvanceCredentialGeneration() => Mutate(current => current with { CredentialGeneration = checked(current.CredentialGeneration + 1) });
    public void AdvanceAuthorizationVersion() => Mutate(current => current with { AuthorizationVersion = checked(current.AuthorizationVersion + 1) });
    public void AdvanceEntitlementVersion() => Mutate(current => current with { EntitlementVersion = checked(current.EntitlementVersion + 1) });
    public void SetDisposition(DwsLocalAuthorizationDisposition disposition) => Mutate(current => current with { Disposition = disposition });

    internal DwsLocalFu16FixtureSnapshot Snapshot()
    {
        lock (_gate) return RequireSnapshot();
    }

    private void Mutate(Func<DwsLocalFu16FixtureSnapshot, DwsLocalFu16FixtureSnapshot> mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        lock (_gate) _snapshot = mutation(RequireSnapshot());
    }

    private DwsLocalFu16FixtureSnapshot RequireSnapshot() =>
        _snapshot ?? throw new DwsValidationException(DwsErrors.AuthorizationAuthorityUnavailable);
}

public sealed class LocalTestFu16FunctionalAuthorization(DwsLocalFu16Fixture fixture)
    : IFu16DwsFunctionalAuthorization
{
    public Task<DwsFu16AuthorizationSnapshot> AuthorizeAsync(
        DwsTrustedActorContext context,
        string moduleCode,
        string moduleEntitlementCode,
        string operation,
        string permission,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DwsLocalTrustedIdentity.Require(context);
        var exactPermission = DwsAuthorizationManifest.RequireExact(operation);
        if (!string.Equals(moduleCode, DwsFunctionalAuthorizationBinding.ModuleCode, StringComparison.Ordinal)
            || !string.Equals(moduleEntitlementCode, DwsFunctionalAuthorizationBinding.ModuleEntitlementCode, StringComparison.Ordinal))
            throw new DwsValidationException(DwsErrors.AuthorizationAuthorityUnavailable);
        if (!string.Equals(exactPermission, permission, StringComparison.Ordinal))
            throw new DwsValidationException(DwsErrors.PermissionDenied);

        var snapshot = fixture.Snapshot();
        if (!SameContext(snapshot.Context, context))
            throw new DwsValidationException(DwsErrors.AuthenticationRequired);
        if (!string.Equals(snapshot.ModuleCode, DwsFunctionalAuthorizationBinding.ModuleCode, StringComparison.Ordinal)
            || !string.Equals(snapshot.ModuleEntitlementCode, DwsFunctionalAuthorizationBinding.ModuleEntitlementCode, StringComparison.Ordinal))
            throw new DwsValidationException(DwsErrors.AuthorizationAuthorityUnavailable);
        if (!string.Equals(snapshot.Operation, operation, StringComparison.Ordinal)
            || !string.Equals(snapshot.Permission, permission, StringComparison.Ordinal))
            throw new DwsValidationException(DwsErrors.PermissionDenied);
        if (!snapshot.HasExplicitTenantGrant)
            throw new DwsValidationException(DwsErrors.PermissionDenied);

        switch (snapshot.Disposition)
        {
            case DwsLocalAuthorizationDisposition.Accepted:
                return Task.FromResult(Capture(snapshot));
            case DwsLocalAuthorizationDisposition.AuthenticationInvalid:
                throw new DwsValidationException(DwsErrors.AuthenticationRequired);
            case DwsLocalAuthorizationDisposition.EntitlementDenied:
            case DwsLocalAuthorizationDisposition.ExplicitGrantDenied:
            case DwsLocalAuthorizationDisposition.PermissionDenied:
                throw new DwsValidationException(DwsErrors.PermissionDenied);
            case DwsLocalAuthorizationDisposition.Unavailable:
            case DwsLocalAuthorizationDisposition.Timeout:
            case DwsLocalAuthorizationDisposition.Malformed:
            case DwsLocalAuthorizationDisposition.Indeterminate:
            case DwsLocalAuthorizationDisposition.StaleFreshness:
                throw new DwsValidationException(DwsErrors.AuthorizationAuthorityUnavailable);
            default:
                throw new DwsValidationException(DwsErrors.AuthorizationAuthorityUnavailable);
        }
    }

    public Task RevalidateAsync(
        DwsTrustedActorContext context,
        DwsFu16AuthorizationSnapshot captured,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DwsLocalTrustedIdentity.Require(context);
        ArgumentNullException.ThrowIfNull(captured);

        if (!SameContext(captured, context)
            || !string.Equals(captured.ModuleCode, DwsFunctionalAuthorizationBinding.ModuleCode, StringComparison.Ordinal)
            || !string.Equals(captured.ModuleEntitlementCode, DwsFunctionalAuthorizationBinding.ModuleEntitlementCode, StringComparison.Ordinal)
            || !string.Equals(DwsAuthorizationManifest.RequireExact(captured.Operation), captured.Permission, StringComparison.Ordinal)
            || !captured.HasExplicitTenantGrant)
            throw new DwsValidationException(DwsErrors.AuthorizationAuthorityUnavailable);

        var current = fixture.Snapshot();
        if (current.Disposition is DwsLocalAuthorizationDisposition.AuthenticationInvalid)
            throw new DwsValidationException(DwsErrors.AuthenticationRequired);
        if (current.Disposition is DwsLocalAuthorizationDisposition.EntitlementDenied
            or DwsLocalAuthorizationDisposition.ExplicitGrantDenied
            or DwsLocalAuthorizationDisposition.PermissionDenied)
            throw new DwsValidationException(DwsErrors.PermissionDenied);
        if (current.Disposition != DwsLocalAuthorizationDisposition.Accepted
            || !SameContext(current.Context, context)
            || !string.Equals(current.ModuleCode, captured.ModuleCode, StringComparison.Ordinal)
            || !string.Equals(current.ModuleEntitlementCode, captured.ModuleEntitlementCode, StringComparison.Ordinal)
            || !string.Equals(current.Operation, captured.Operation, StringComparison.Ordinal)
            || !string.Equals(current.Permission, captured.Permission, StringComparison.Ordinal)
            || current.HasExplicitTenantGrant != captured.HasExplicitTenantGrant
            || current.PrincipalGeneration != captured.PrincipalGeneration
            || current.CredentialGeneration != captured.CredentialGeneration
            || current.AuthorizationVersion != captured.AuthorizationVersion
            || current.EntitlementVersion != captured.EntitlementVersion)
            throw new DwsValidationException(DwsErrors.AuthorizationAuthorityUnavailable);
        return Task.CompletedTask;
    }

    private static DwsFu16AuthorizationSnapshot Capture(DwsLocalFu16FixtureSnapshot snapshot) => new(
        snapshot.Context.TenantId,
        snapshot.Context.SecuritySubjectId,
        snapshot.Context.EffectiveActorId,
        snapshot.Context.DelegatedActorId,
        snapshot.ModuleCode,
        snapshot.ModuleEntitlementCode,
        snapshot.Operation,
        snapshot.Permission,
        snapshot.HasExplicitTenantGrant,
        snapshot.PrincipalGeneration,
        snapshot.CredentialGeneration,
        snapshot.AuthorizationVersion,
        snapshot.EntitlementVersion);

    private static bool SameContext(DwsFu16AuthorizationSnapshot expected, DwsTrustedActorContext actual) =>
        expected.TenantId == actual.TenantId
        && expected.SecuritySubjectId == actual.SecuritySubjectId
        && expected.EffectiveActorId == actual.EffectiveActorId
        && expected.DelegatedActorId == actual.DelegatedActorId;

    private static bool SameContext(DwsTrustedActorContext expected, DwsTrustedActorContext actual) =>
        expected.TenantId == actual.TenantId
        && expected.SecuritySubjectId == actual.SecuritySubjectId
        && expected.EffectiveActorId == actual.EffectiveActorId
        && expected.DelegatedActorId == actual.DelegatedActorId;
}

public sealed record DwsLocalAuditObservation(
    long AuditIntentCount,
    long OutboxCount,
    bool IsDeliverable,
    bool IsAuthoritativelyAccepted);

public interface IDwsLocalAuditObserver
{
    Task<DwsLocalAuditObservation> ObserveAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

public sealed class LocalTestDwsAuditObserver(DwsMongoContext context) : IDwsLocalAuditObserver
{
    public async Task<DwsLocalAuditObservation> ObserveAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new DwsValidationException(DwsErrors.InvalidRequest);

        var tenant = new BsonBinaryData(tenantId, GuidRepresentation.Standard);
        var filter = new BsonDocument
        {
            ["TenantId"] = tenant,
            ["IsDeleted"] = false
        };
        var auditCount = await context.Collection("audit-intents").CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var outboxCount = await context.Collection("outbox").CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        return new(auditCount, outboxCount, IsDeliverable: false, IsAuthoritativelyAccepted: false);
    }
}

internal static class DwsLocalTrustedIdentity
{
    public static void Require(DwsTrustedActorContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.TenantId == Guid.Empty
            || context.SecuritySubjectId == Guid.Empty
            || context.EffectiveActorId == Guid.Empty
            || context.DelegatedActorId == Guid.Empty
            || context.DelegatedActorId == context.SecuritySubjectId
            || context.DelegatedActorId == context.EffectiveActorId)
            throw new DwsValidationException(DwsErrors.AuthenticationRequired);
    }
}
