using System.Security.Cryptography;
using System.Text;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Domain.Repositories;

namespace Diten.AuthService.Application.Features.ServiceIdentityTokens.Operational;

/// <summary>Bounded reuse of Section E: existing identities only; no grant, catalog or startup writes.</summary>
public sealed class ServiceClientOperationalProvisioningService : IServiceClientOperationalProvisioningService
{
    private static readonly Guid PlatformTenant = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private readonly IServiceClientIdentityRepository _identities;
    private readonly IServiceClientTenantGrantRepository _grants;
    private readonly IServiceClientOperationalProvisioningOperationRepository _operations;
    private readonly IServiceClientCredentialVerifier _credentials;
    private readonly IServiceClientSecretOutputSink _sink;
    private readonly TimeProvider _clock;

    public ServiceClientOperationalProvisioningService(IServiceClientIdentityRepository identities,
        IServiceClientTenantGrantRepository grants, IServiceClientOperationalProvisioningOperationRepository operations,
        IServiceClientCredentialVerifier credentials, IServiceClientSecretOutputSink sink, TimeProvider clock)
    {
        _identities = identities;
        _grants = grants;
        _operations = operations;
        _credentials = credentials;
        _sink = sink;
        _clock = clock;
    }

    public async Task<ServiceClientOperationalProvisioningResult> ExecuteAsync(
        ServiceClientOperationalProvisioningRequest request, ServiceClientOperationalActor actor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actor);
        ValidateRequest(request, actor);
        await _operations.VerifyStorageAsync(cancellationToken);
        if (!ServiceClientOperationalOperations.IsMutation(request.Operation))
            return await ReadAsync(request, cancellationToken);

        var fingerprint = Fingerprint(request, actor);
        var existing = await _operations.GetByCommandIdAsync(request.CommandId, cancellationToken);
        if (existing is not null)
            return await ReplayAsync(existing, request, actor, fingerprint, cancellationToken);

        var identity = await RequireIdentityAsync(request, cancellationToken);
        EnsureExpectedVersion(identity, request);
        var now = _clock.GetUtcNow();
        var operation = new ServiceClientOperationalProvisioningOperation
        {
            CommandId = request.CommandId, CommandFingerprint = fingerprint, Operation = request.Operation,
            TargetId = identity.Id, ClientCode = identity.ClientCode, ServiceName = identity.ServiceName,
            Audience = request.Audience!, ExpectedOperationalVersion = request.ExpectedOperationalVersion,
            ExpectedCredentialVersion = request.ExpectedCredentialVersion!, ActorId = actor.UserId.ToString("D"),
            State = ServiceClientOperationalProvisioningState.Pending,
            Checkpoint = ServiceClientOperationalProvisioningCheckpoint.Reserved,
            CreatedAt = now, CreatedBy = actor.UserId.ToString("D"), EvidenceSequence = 1,
            Evidence = [new ServiceClientOperationalProvisioningEvidence
            {
                Sequence = 1, EventType = "reserved", OccurredAtUtc = now,
                ActorId = actor.UserId.ToString("D"), TargetOperationalVersion = identity.OperationalVersion
            }]
        };
        if (!await _operations.TryReserveAsync(operation, cancellationToken))
        {
            existing = await _operations.GetByCommandIdAsync(request.CommandId, cancellationToken)
                ?? throw new ServiceClientOperationalRecoveryRequiredException("Operational reservation is uncertain.");
            return await ReplayAsync(existing, request, actor, fingerprint, cancellationToken);
        }

        string? rawSecret = null;
        ServiceClientOperationalRecordedOutcome outcome;
        try
        {
            var entropy = RandomNumberGenerator.GetBytes(32);
            try { rawSecret = Convert.ToBase64String(entropy).TrimEnd('=').Replace('+', '-').Replace('/', '_'); }
            finally { CryptographicOperations.ZeroMemory(entropy); }
            var newHash = _credentials.Hash(rawSecret);
            var credentialVersion = $"op-{request.CommandId:N}";
            var mutation = new ServiceClientCredentialRotation(identity.Id, identity.ClientCode, identity.ServiceName,
                identity.AllowedAudience, request.ExpectedOperationalVersion, identity.ActiveCredentialHash,
                identity.ActiveCredentialVersion, newHash, credentialVersion, now.AddSeconds(300), now,
                actor.UserId.ToString("D"), request.CommandId, fingerprint);
            var result = await _identities.RotateCredentialOperationalAsync(mutation, cancellationToken);
            if (result.Status != OperationalMutationStatus.Applied)
                throw new ServiceClientOperationalConflictException("Operational target changed before rotation.");

            var persisted = await RequireIdentityAsync(request, cancellationToken);
            if (persisted.OperationalVersion != checked(request.ExpectedOperationalVersion + 1)
                || persisted.LastOperationalCommandId != request.CommandId
                || !Same(persisted.LastOperationalCommandFingerprint, fingerprint)
                || !Same(persisted.ActiveCredentialVersion, credentialVersion)
                || !Same(persisted.ActiveCredentialHash, newHash)
                || !Same(persisted.PreviousCredentialHash, identity.ActiveCredentialHash)
                || !Same(persisted.PreviousCredentialVersion, identity.ActiveCredentialVersion)
                || persisted.PreviousValidUntilUtc != mutation.PreviousValidUntilUtc)
                throw new ServiceClientOperationalRecoveryRequiredException("Rotation read-back is inconsistent.");

            outcome = new ServiceClientOperationalRecordedOutcome
            {
                ServiceClientIdentityId = persisted.Id, ClientCode = persisted.ClientCode,
                ServiceName = persisted.ServiceName, Audience = request.Audience!,
                OperationalVersion = persisted.OperationalVersion, CredentialVersion = persisted.ActiveCredentialVersion,
                MutationObservedAtUtc = _clock.GetUtcNow()
            };
            if (!await _operations.TryCompleteAsync(request.CommandId, fingerprint, outcome, _clock.GetUtcNow(), cancellationToken))
                throw new ServiceClientOperationalRecoveryRequiredException("Operational completion is uncertain.");
            var completed = await _operations.GetByCommandIdAsync(request.CommandId, cancellationToken)
                ?? throw new ServiceClientOperationalRecoveryRequiredException("Operational completion is missing.");
            ValidateOperation(completed, request, actor, fingerprint);
            ValidateCompleted(completed);
            if (completed.RecordedOutcome != outcome)
                throw new ServiceClientOperationalRecoveryRequiredException("Operational recorded outcome drift.");
        }
        catch (Exception exception)
        {
            // A pending/ambiguous operation is never auto-resumed. A later command needs independent approval.
            using var recoveryBudget = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            try { await _operations.TryMarkRecoveryRequiredAsync(request.CommandId, fingerprint, recoveryBudget.Token); }
            catch { /* Preserve the original failure; no optimistic success is returned. */ }
            if (exception is OperationCanceledException or ServiceClientOperationalConflictException)
                throw;
            throw new ServiceClientOperationalRecoveryRequiredException("Operational rotation requires manual reconciliation.");
        }

        try
        {
            await _sink.DeliverOnceAsync(outcome.ServiceClientIdentityId, outcome.ClientCode, rawSecret!, cancellationToken);
        }
        catch (Exception)
        {
            // The durable credential outcome remains true, but there is no consumer receipt. Never re-emit this secret.
            throw new ServiceClientOperationalSecretDeliveryException("Credential mutation completed; secret delivery is uncertain.");
        }
        finally { rawSecret = null; }
        return FromOutcome(operation, outcome, false, "sink-written-once", null);
    }

    private async Task<ServiceClientOperationalProvisioningResult> ReadAsync(
        ServiceClientOperationalProvisioningRequest request, CancellationToken ct)
    {
        var identity = await RequireIdentityAsync(request, ct);
        EnsureExpectedVersion(identity, request);
        bool? grantEnabled = null;
        if (request.Operation == ServiceClientOperationalOperations.ReadGrant)
        {
            if (!await _grants.HasEnabledGrantAsync(request.TenantId!.Value, identity.Id, request.Audience!, ct))
                throw new ServiceClientOperationalNotFoundException("An enabled exact tenant grant was not found.");
            grantEnabled = true;
        }
        return new(request.Operation, identity.Id, request.TenantId, identity.ClientCode, identity.ServiceName,
            request.Audience!, identity.OperationalVersion, identity.ActiveCredentialVersion, string.Empty,
            false, "not-applicable", grantEnabled);
    }

    private async Task<ServiceClientOperationalProvisioningResult> ReplayAsync(
        ServiceClientOperationalProvisioningOperation operation, ServiceClientOperationalProvisioningRequest request,
        ServiceClientOperationalActor actor, string fingerprint, CancellationToken ct)
    {
        ValidateOperation(operation, request, actor, fingerprint);
        ValidateCompleted(operation);
        var current = await RequireIdentityAsync(request, ct);
        // Deliberately do not compare the old expected version with the current version here.
        return FromOutcome(operation, operation.RecordedOutcome!, true, "not-reissued", current);
    }

    private static ServiceClientOperationalProvisioningResult FromOutcome(
        ServiceClientOperationalProvisioningOperation operation, ServiceClientOperationalRecordedOutcome outcome,
        bool replay, string secretDisposition, ServiceClientIdentity? current) =>
        new(operation.Operation, outcome.ServiceClientIdentityId, operation.TenantId, outcome.ClientCode,
            outcome.ServiceName, outcome.Audience, outcome.OperationalVersion, outcome.CredentialVersion,
            operation.CommandFingerprint, replay, secretDisposition, null,
            current?.OperationalVersion, current?.ActiveCredentialVersion);

    private async Task<ServiceClientIdentity> RequireIdentityAsync(ServiceClientOperationalProvisioningRequest request, CancellationToken ct)
    {
        var identity = await _identities.GetByIdAsync(request.ServiceClientIdentityId!.Value, ct)
            ?? throw new ServiceClientOperationalNotFoundException("Service client identity was not found.");
        if (identity.Id != request.ServiceClientIdentityId || identity.IsDeleted || identity.IsRevoked
            || identity.OperationalVersion < 0 || !_credentials.IsStateCoherent(identity)
            || !Same(identity.ClientCode, request.ClientCode) || !Same(identity.ServiceName, request.ServiceName)
            || !ServiceIdentityTokenAudiencePolicy.TryResolveIdentityAudience(identity.AllowedAudience, out var purpose)
            || !Same(purpose, request.Audience))
            throw new ServiceClientOperationalConflictException("Service client identity or purpose is inconsistent.");
        return identity;
    }

    private static void EnsureExpectedVersion(ServiceClientIdentity identity, ServiceClientOperationalProvisioningRequest request)
    {
        if (identity.OperationalVersion != request.ExpectedOperationalVersion
            || (request.ExpectedCredentialVersion is not null && !Same(identity.ActiveCredentialVersion, request.ExpectedCredentialVersion)))
            throw new ServiceClientOperationalConflictException("Service client expected version is stale.");
    }

    private static void ValidateOperation(ServiceClientOperationalProvisioningOperation operation,
        ServiceClientOperationalProvisioningRequest request, ServiceClientOperationalActor actor, string fingerprint)
    {
        if (operation.IsDeleted || operation.CommandId != request.CommandId || !Same(operation.CommandFingerprint, fingerprint)
            || !Same(operation.Operation, request.Operation) || operation.TargetId != request.ServiceClientIdentityId
            || operation.TenantId != request.TenantId || !Same(operation.ClientCode, request.ClientCode)
            || !Same(operation.ServiceName, request.ServiceName) || !Same(operation.Audience, request.Audience)
            || operation.ExpectedOperationalVersion != request.ExpectedOperationalVersion
            || !Same(operation.ExpectedCredentialVersion, request.ExpectedCredentialVersion)
            || !Same(operation.ActorId, actor.UserId.ToString("D")))
            throw new ServiceClientOperationalConflictException("Operational command fingerprint or target drift.");
    }

    private static void ValidateCompleted(ServiceClientOperationalProvisioningOperation operation)
    {
        var outcome = operation.RecordedOutcome;
        if (operation.State != ServiceClientOperationalProvisioningState.Completed
            || operation.Checkpoint != ServiceClientOperationalProvisioningCheckpoint.EvidenceRecorded
            || outcome is null || outcome.ServiceClientIdentityId != operation.TargetId
            || !Same(outcome.ClientCode, operation.ClientCode) || !Same(outcome.ServiceName, operation.ServiceName)
            || !Same(outcome.Audience, operation.Audience)
            || operation.ExpectedOperationalVersion == long.MaxValue
            || outcome.OperationalVersion != operation.ExpectedOperationalVersion + 1
            || !Same(outcome.CredentialVersion, $"op-{operation.CommandId:N}")
            || outcome.MutationObservedAtUtc == default || outcome.MutationObservedAtUtc.Offset != TimeSpan.Zero)
            throw new ServiceClientOperationalRecoveryRequiredException("Operational command requires manual reconciliation.");
    }

    private static void ValidateRequest(ServiceClientOperationalProvisioningRequest request, ServiceClientOperationalActor actor)
    {
        if (!ServiceClientOperationalOperations.IsSupported(request.Operation) || request.CommandId == Guid.Empty
            || request.ServiceClientIdentityId is null || request.ServiceClientIdentityId == Guid.Empty
            || request.ExpectedOperationalVersion < 0 || request.ExpectedOperationalVersion == long.MaxValue
            || actor.UserId == Guid.Empty || actor.TenantId != PlatformTenant || !Same(actor.ActorType, "platform_admin"))
            throw new ServiceClientOperationalContractException("Operational command or authorized actor is invalid.");
        RequireText(request.ClientCode, 128);
        RequireText(request.ServiceName, 128);
        RequireText(request.Audience, 128);
        if (!ServiceIdentityTokenAudiencePolicy.IsAllowedPair(request.ServiceName!, request.Audience!))
            throw new ServiceClientOperationalContractException("Service client purpose is not supported.");
        if (request.ExpectedCredentialVersion is not null) RequireText(request.ExpectedCredentialVersion, 128);
        if (request.Operation == ServiceClientOperationalOperations.ReadGrant)
        {
            if (request.TenantId is null || request.TenantId == Guid.Empty)
                throw new ServiceClientOperationalContractException("An exact grant tenant is required.");
        }
        else if (request.TenantId is not null)
            throw new ServiceClientOperationalContractException("Identity operations are global, not tenant-scoped.");
        if (ServiceClientOperationalOperations.EmitsSecret(request.Operation))
        {
            RequireText(request.ExpectedCredentialVersion, 128);
            RequireText(request.InheritedPipeHandle, 128);
        }
        else if (request.InheritedPipeHandle is not null)
            throw new ServiceClientOperationalContractException("Read operations cannot emit a credential.");
    }

    private static void RequireText(string? value, int limit)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > limit || !Same(value, value.Trim()) || value.Any(char.IsControl))
            throw new ServiceClientOperationalContractException("Operational text is invalid.");
    }

    private static string Fingerprint(ServiceClientOperationalProvisioningRequest request, ServiceClientOperationalActor actor)
    {
        // Length-prefixed UTF-8 fields avoid delimiter ambiguity. The sink handle is delivery-only, not command identity.
        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write("service-client-rotation.v2"); writer.Write(request.Operation); writer.Write(request.CommandId.ToString("D"));
            writer.Write(request.ExpectedOperationalVersion); writer.Write(request.ExpectedCredentialVersion ?? string.Empty);
            writer.Write(request.ServiceClientIdentityId!.Value.ToString("D")); writer.Write(request.TenantId?.ToString("D") ?? string.Empty);
            writer.Write(request.ClientCode!); writer.Write(request.ServiceName!); writer.Write(request.Audience!);
            writer.Write(actor.UserId.ToString("D")); writer.Write(actor.TenantId.ToString("D")); writer.Write(actor.ActorType);
            writer.Write(300);
        }
        return Convert.ToHexString(SHA256.HashData(buffer.ToArray())).ToLowerInvariant();
    }

    private static bool Same(string? left, string? right) => string.Equals(left, right, StringComparison.Ordinal);
}
