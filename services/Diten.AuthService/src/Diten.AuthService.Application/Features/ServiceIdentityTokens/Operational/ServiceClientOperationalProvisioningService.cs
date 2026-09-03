using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Domain.Repositories;

namespace Diten.AuthService.Application.Features.ServiceIdentityTokens.Operational;

public sealed class ServiceClientOperationalProvisioningService : IServiceClientOperationalProvisioningService
{
    private const int CredentialBytes = 32;
    private static readonly TimeSpan CredentialOverlap = TimeSpan.FromSeconds(300);

    private readonly IServiceClientIdentityRepository _identities;
    private readonly IServiceClientTenantGrantRepository _grants;
    private readonly IServiceClientOperationalProvisioningOperationRepository _operations;
    private readonly IServiceClientCredentialVerifier _credentialVerifier;
    private readonly IServiceClientSecretOutputSink _secretSink;
    private readonly TimeProvider _timeProvider;

    public ServiceClientOperationalProvisioningService(
        IServiceClientIdentityRepository identities,
        IServiceClientTenantGrantRepository grants,
        IServiceClientOperationalProvisioningOperationRepository operations,
        IServiceClientCredentialVerifier credentialVerifier,
        IServiceClientSecretOutputSink secretSink,
        TimeProvider timeProvider)
    {
        _identities = identities;
        _grants = grants;
        _operations = operations;
        _credentialVerifier = credentialVerifier;
        _secretSink = secretSink;
        _timeProvider = timeProvider;
    }

    public async Task<ServiceClientOperationalProvisioningResult> ExecuteAsync(
        ServiceClientOperationalProvisioningRequest request,
        ServiceClientOperationalActor actor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateCommon(request, actor);

        if (request.Operation == ServiceClientOperationalOperations.ReadIdentity)
            return await ReadIdentityAsync(request, cancellationToken);
        if (request.Operation == ServiceClientOperationalOperations.ReadGrant)
            return await ReadGrantAsync(request, cancellationToken);

        var normalized = NormalizeMutation(request);
        var fingerprint = Fingerprint(normalized, actor);
        var operation = await _operations.GetByCommandIdAsync(normalized.CommandId, cancellationToken);
        if (operation is null)
        {
            await PrevalidateMutationTargetAsync(normalized, cancellationToken);
            operation = await ReserveOrLoadAsync(normalized, actor, fingerprint, cancellationToken);
        }
        if (!string.Equals(operation.CommandFingerprint, fingerprint, StringComparison.Ordinal))
            throw new ServiceClientOperationalConflictException("Operational command fingerprint drift.");

        if (operation.State == ServiceClientOperationalProvisioningState.Completed)
            return await ReadCompletedResultAsync(operation, true, cancellationToken);

        string? rawSecret = null;
        var appliedByThisInvocation = false;
        try
        {
            if (operation.Checkpoint == ServiceClientOperationalProvisioningCheckpoint.Reserved)
            {
                var mutation = await MutateAsync(normalized, operation, actor, fingerprint, cancellationToken);
                rawSecret = mutation.RawSecret;
                appliedByThisInvocation = mutation.Applied;
                await AdvanceOrObserveAsync(operation, ServiceClientOperationalProvisioningCheckpoint.Reserved,
                    ServiceClientOperationalProvisioningCheckpoint.TargetMutated,
                    ServiceClientOperationalProvisioningState.Pending, "target-mutated",
                    mutation.OperationalVersion, actor, cancellationToken);
                operation = await LoadExactOperationAsync(operation.CommandId, fingerprint, cancellationToken);
            }

            if (operation.Checkpoint == ServiceClientOperationalProvisioningCheckpoint.TargetMutated)
            {
                var targetVersion = await VerifyReadBackAsync(normalized, operation, fingerprint, cancellationToken);
                await AdvanceOrObserveAsync(operation, ServiceClientOperationalProvisioningCheckpoint.TargetMutated,
                    ServiceClientOperationalProvisioningCheckpoint.ReadBackVerified,
                    ServiceClientOperationalProvisioningState.Pending, "read-back-verified",
                    targetVersion, actor, cancellationToken);
                operation = await LoadExactOperationAsync(operation.CommandId, fingerprint, cancellationToken);
            }

            if (operation.Checkpoint == ServiceClientOperationalProvisioningCheckpoint.ReadBackVerified)
            {
                var targetVersion = await VerifyReadBackAsync(normalized, operation, fingerprint, cancellationToken);
                await AdvanceOrObserveAsync(operation, ServiceClientOperationalProvisioningCheckpoint.ReadBackVerified,
                    ServiceClientOperationalProvisioningCheckpoint.EvidenceRecorded,
                    ServiceClientOperationalProvisioningState.Completed, "completed",
                    targetVersion, actor, cancellationToken);
                operation = await LoadExactOperationAsync(operation.CommandId, fingerprint, cancellationToken);
            }

            if (operation.State != ServiceClientOperationalProvisioningState.Completed)
                throw new ServiceClientOperationalRecoveryRequiredException("Operational evidence did not complete.");

            if (rawSecret is not null && appliedByThisInvocation)
            {
                try
                {
                    await _secretSink.DeliverOnceAsync(
                        operation.TargetId, operation.ClientCode, rawSecret, cancellationToken);
                }
                catch (Exception ex)
                {
                    throw new ServiceClientOperationalSecretDeliveryException(
                        "Secret delivery did not complete; a new authorized credential rotation is required.", ex);
                }
            }

            return await ReadCompletedResultAsync(operation, !appliedByThisInvocation, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not ServiceClientOperationalContractException
                                   and not ServiceClientOperationalConflictException
                                   and not ServiceClientOperationalNotFoundException)
        {
            await TryMarkRecoveryRequiredAsync(operation, cancellationToken);
            throw ex is ServiceClientOperationalRecoveryRequiredException
                ? ex
                : new ServiceClientOperationalRecoveryRequiredException(
                    "Operational provisioning requires recovery.", ex);
        }
    }

    private async Task<ServiceClientOperationalProvisioningResult> ReadIdentityAsync(
        ServiceClientOperationalProvisioningRequest request, CancellationToken ct)
    {
        ServiceClientIdentity? identity;
        if (request.ServiceClientIdentityId is { } id && id != Guid.Empty)
            identity = await _identities.GetByIdAsync(id, ct);
        else
            identity = await _identities.GetByClientCodeAsync(Required(request.ClientCode, "Client code"), ct);
        if (identity is null) throw new ServiceClientOperationalNotFoundException("Service client identity was not found.");
        return IdentityResult(request.Operation, identity, string.Empty, false, "not-applicable");
    }

    private async Task<ServiceClientOperationalProvisioningResult> ReadGrantAsync(
        ServiceClientOperationalProvisioningRequest request, CancellationToken ct)
    {
        var tenantId = Required(request.TenantId, "Tenant id");
        var identityId = Required(request.ServiceClientIdentityId, "Service client identity id");
        var audience = Required(request.Audience, "Audience");
        var identity = await RequireIdentityAsync(identityId, ct);
        var grant = await _grants.GetAsync(tenantId, identityId, audience, ct)
            ?? throw new ServiceClientOperationalNotFoundException("Service client tenant grant was not found.");
        return GrantResult(request.Operation, identity, grant, string.Empty, false);
    }

    private static ServiceClientOperationalProvisioningRequest NormalizeMutation(
        ServiceClientOperationalProvisioningRequest request)
    {
        if (request.CommandId == Guid.Empty)
            throw new ServiceClientOperationalContractException("Command id is required.");
        if (request.ExpectedOperationalVersion < 0)
            throw new ServiceClientOperationalContractException("Expected operational version is invalid.");

        var clientCode = request.ClientCode;
        if (request.Operation == ServiceClientOperationalOperations.CreateIdentity)
        {
            clientCode = Required(clientCode, "Client code").Trim().ToUpperInvariant();
            ValidateBoundedText(clientCode, "Client code", 128);
        }

        return request with { ClientCode = clientCode };
    }

    private async Task<ServiceClientOperationalProvisioningOperation> ReserveOrLoadAsync(
        ServiceClientOperationalProvisioningRequest request,
        ServiceClientOperationalActor actor,
        string fingerprint,
        CancellationToken ct)
    {
        var current = await _operations.GetByCommandIdAsync(request.CommandId, ct);
        if (current is not null) return current;

        var targetId = request.Operation == ServiceClientOperationalOperations.CreateIdentity
            ? Guid.NewGuid()
            : Required(request.ServiceClientIdentityId, "Service client identity id");
        var now = _timeProvider.GetUtcNow();
        var operation = new ServiceClientOperationalProvisioningOperation
        {
            CommandId = request.CommandId,
            CommandFingerprint = fingerprint,
            Operation = request.Operation,
            TargetId = targetId,
            TenantId = request.TenantId,
            ClientCode = request.ClientCode ?? string.Empty,
            ServiceName = request.ServiceName ?? string.Empty,
            Audience = request.Audience ?? string.Empty,
            ExpectedOperationalVersion = request.ExpectedOperationalVersion,
            ActorId = actor.UserId.ToString("D"),
            State = ServiceClientOperationalProvisioningState.Pending,
            Checkpoint = ServiceClientOperationalProvisioningCheckpoint.Reserved,
            CreatedAt = now,
            CreatedBy = actor.UserId.ToString("D")
        };
        if (await _operations.TryReserveAsync(operation, ct)) return operation;
        return await _operations.GetByCommandIdAsync(request.CommandId, ct)
            ?? throw new ServiceClientOperationalConflictException("Operational command reservation conflict.");
    }

    private async Task PrevalidateMutationTargetAsync(
        ServiceClientOperationalProvisioningRequest request, CancellationToken ct)
    {
        if (request.Operation == ServiceClientOperationalOperations.CreateIdentity)
        {
            if (request.ExpectedOperationalVersion != 0)
                throw new ServiceClientOperationalConflictException("Identity creation requires expected version zero.");
            var existing = await _identities.GetByClientCodeAsync(Required(request.ClientCode, "Client code"), ct);
            if (existing is not null)
                throw new ServiceClientOperationalConflictException("Service client code already exists.");
            var serviceName = Required(request.ServiceName, "Service name");
            var createAudience = Required(request.Audience, "Audience");
            if (!ServiceIdentityTokenAudiencePolicy.IsAllowedPair(serviceName, createAudience))
                throw new ServiceClientOperationalContractException("Service and audience purpose is not allowed.");
            return;
        }

        var identityId = Required(request.ServiceClientIdentityId, "Service client identity id");
        var identity = await RequireIdentityAsync(identityId, ct);
        EnsurePurpose(identity, request);
        if (identity.OperationalVersion != request.ExpectedOperationalVersion
            && request.Operation is ServiceClientOperationalOperations.RotateCredential
                or ServiceClientOperationalOperations.RevokeIdentity)
            throw new ServiceClientOperationalConflictException("Identity operational version is stale.");
        if (identity.IsRevoked)
            throw new ServiceClientOperationalConflictException("Revoked identity operation is forbidden.");

        if (request.Operation is not ServiceClientOperationalOperations.EnableGrant
            and not ServiceClientOperationalOperations.DisableGrant) return;

        var tenantId = Required(request.TenantId, "Tenant id");
        var audience = Required(request.Audience, "Audience");
        var grant = await _grants.GetAsync(tenantId, identityId, audience, ct);
        if (grant is null)
        {
            if (request.Operation == ServiceClientOperationalOperations.DisableGrant)
                throw new ServiceClientOperationalNotFoundException("Service client tenant grant was not found.");
            if (request.ExpectedOperationalVersion != 0)
                throw new ServiceClientOperationalConflictException("Grant creation requires expected version zero.");
            return;
        }

        if (grant.OperationalVersion != request.ExpectedOperationalVersion)
            throw new ServiceClientOperationalConflictException("Grant operational version is stale.");
    }

    private async Task<MutationOutcome> MutateAsync(
        ServiceClientOperationalProvisioningRequest request,
        ServiceClientOperationalProvisioningOperation operation,
        ServiceClientOperationalActor actor,
        string fingerprint,
        CancellationToken ct)
    {
        var now = _timeProvider.GetUtcNow();
        return request.Operation switch
        {
            ServiceClientOperationalOperations.CreateIdentity =>
                await CreateIdentityAsync(request, operation, actor, fingerprint, now, ct),
            ServiceClientOperationalOperations.RotateCredential =>
                await RotateCredentialAsync(request, operation, actor, fingerprint, now, ct),
            ServiceClientOperationalOperations.RevokeIdentity =>
                await RevokeIdentityAsync(request, operation, actor, fingerprint, now, ct),
            ServiceClientOperationalOperations.EnableGrant =>
                await SetGrantAsync(request, operation, actor, fingerprint, true, now, ct),
            ServiceClientOperationalOperations.DisableGrant =>
                await SetGrantAsync(request, operation, actor, fingerprint, false, now, ct),
            _ => throw new ServiceClientOperationalContractException("Unsupported operational mutation.")
        };
    }

    private async Task<MutationOutcome> CreateIdentityAsync(
        ServiceClientOperationalProvisioningRequest request,
        ServiceClientOperationalProvisioningOperation operation,
        ServiceClientOperationalActor actor,
        string fingerprint,
        DateTimeOffset now,
        CancellationToken ct)
    {
        if (request.ExpectedOperationalVersion != 0)
            throw new ServiceClientOperationalConflictException("Identity creation requires expected version zero.");
        var serviceName = Required(request.ServiceName, "Service name");
        var createAudience = Required(request.Audience, "Audience");
        if (!ServiceIdentityTokenAudiencePolicy.IsAllowedPair(serviceName, createAudience))
            throw new ServiceClientOperationalContractException("Service and audience purpose is not allowed.");

        var rawSecret = GenerateSecret();
        var identity = new ServiceClientIdentity
        {
            Id = operation.TargetId,
            ClientCode = Required(request.ClientCode, "Client code"),
            ServiceName = serviceName,
            AllowedAudience = createAudience,
            ActiveCredentialHash = _credentialVerifier.Hash(rawSecret),
            ActiveCredentialVersion = CredentialVersion(request.CommandId),
            OperationalVersion = 1,
            LastOperationalCommandId = request.CommandId,
            LastOperationalCommandFingerprint = fingerprint,
            CreatedAt = now,
            CreatedBy = actor.UserId.ToString("D")
        };
        var mutation = await _identities.CreateOperationalAsync(identity, request.CommandId, fingerprint, ct);
        return ResolveMutation(mutation, rawSecret);
    }

    private async Task<MutationOutcome> RotateCredentialAsync(
        ServiceClientOperationalProvisioningRequest request,
        ServiceClientOperationalProvisioningOperation operation,
        ServiceClientOperationalActor actor,
        string fingerprint,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var identity = await RequireIdentityAsync(operation.TargetId, ct);
        EnsurePurpose(identity, request);
        if (identity.IsRevoked)
            throw new ServiceClientOperationalConflictException("Revoked identities cannot be rotated.");
        var rawSecret = GenerateSecret();
        var mutation = await _identities.RotateCredentialOperationalAsync(
            identity.Id, request.ExpectedOperationalVersion, request.CommandId, fingerprint,
            identity.ActiveCredentialHash, identity.ActiveCredentialVersion,
            _credentialVerifier.Hash(rawSecret),
            CredentialVersion(request.CommandId), now.Add(CredentialOverlap), now,
            actor.UserId.ToString("D"), ct);
        return ResolveMutation(mutation, rawSecret);
    }

    private async Task<MutationOutcome> RevokeIdentityAsync(
        ServiceClientOperationalProvisioningRequest request,
        ServiceClientOperationalProvisioningOperation operation,
        ServiceClientOperationalActor actor,
        string fingerprint,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var identity = await RequireIdentityAsync(operation.TargetId, ct);
        EnsurePurpose(identity, request);
        var mutation = await _identities.RevokeOperationalAsync(
            identity.Id, request.ExpectedOperationalVersion, request.CommandId, fingerprint,
            now, actor.UserId.ToString("D"), ct);
        return ResolveMutation(mutation, null);
    }

    private async Task<MutationOutcome> SetGrantAsync(
        ServiceClientOperationalProvisioningRequest request,
        ServiceClientOperationalProvisioningOperation operation,
        ServiceClientOperationalActor actor,
        string fingerprint,
        bool enabled,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var tenantId = Required(request.TenantId, "Tenant id");
        var identity = await RequireIdentityAsync(operation.TargetId, ct);
        EnsurePurpose(identity, request);
        if (identity.IsRevoked)
            throw new ServiceClientOperationalConflictException("A grant cannot be enabled or disabled for a revoked identity.");
        var grant = new ServiceClientTenantGrant
        {
            TenantId = tenantId,
            ServiceClientIdentityId = identity.Id,
            Audience = Required(request.Audience, "Audience"),
            IsEnabled = enabled,
            CreatedAt = now,
            CreatedBy = actor.UserId.ToString("D")
        };
        var mutation = await _grants.SetEnabledOperationalAsync(
            grant, enabled, request.ExpectedOperationalVersion, request.CommandId, fingerprint,
            now, actor.UserId.ToString("D"), ct);
        return ResolveMutation(mutation, null);
    }

    private async Task<long> VerifyReadBackAsync(
        ServiceClientOperationalProvisioningRequest request,
        ServiceClientOperationalProvisioningOperation operation,
        string fingerprint,
        CancellationToken ct)
    {
        if (request.Operation is ServiceClientOperationalOperations.EnableGrant
            or ServiceClientOperationalOperations.DisableGrant)
        {
            var grantIdentity = await RequireIdentityAsync(operation.TargetId, ct);
            EnsurePurpose(grantIdentity, request);
            var grant = await _grants.GetAsync(Required(request.TenantId, "Tenant id"), operation.TargetId,
                Required(request.Audience, "Audience"), ct)
                ?? throw new ServiceClientOperationalRecoveryRequiredException("Grant read-back is missing.");
            if (grant.LastOperationalCommandId != request.CommandId
                || !string.Equals(grant.LastOperationalCommandFingerprint, fingerprint, StringComparison.Ordinal)
                || grant.IsEnabled != (request.Operation == ServiceClientOperationalOperations.EnableGrant))
                throw new ServiceClientOperationalRecoveryRequiredException("Grant read-back is inconsistent.");
            return grant.OperationalVersion;
        }

        var identity = await RequireIdentityAsync(operation.TargetId, ct);
        if (identity.LastOperationalCommandId != request.CommandId
            || !string.Equals(identity.LastOperationalCommandFingerprint, fingerprint, StringComparison.Ordinal))
            throw new ServiceClientOperationalRecoveryRequiredException("Identity read-back is inconsistent.");
        if ((!string.IsNullOrEmpty(operation.ClientCode)
                && !string.Equals(identity.ClientCode, operation.ClientCode, StringComparison.Ordinal))
            || !string.Equals(identity.ServiceName, operation.ServiceName, StringComparison.Ordinal)
            || !ServiceIdentityTokenAudiencePolicy.TryResolveIdentityAudience(identity.AllowedAudience, out var purpose)
            || !string.Equals(purpose, operation.Audience, StringComparison.Ordinal))
            throw new ServiceClientOperationalRecoveryRequiredException("Identity purpose read-back is inconsistent.");
        if (request.Operation == ServiceClientOperationalOperations.RevokeIdentity && !identity.IsRevoked)
            throw new ServiceClientOperationalRecoveryRequiredException("Identity revoke read-back is inconsistent.");
        if (request.Operation != ServiceClientOperationalOperations.RevokeIdentity
            && !_credentialVerifier.IsStateCoherent(identity))
            throw new ServiceClientOperationalRecoveryRequiredException("Identity credential read-back is inconsistent.");
        return identity.OperationalVersion;
    }

    private async Task AdvanceOrObserveAsync(
        ServiceClientOperationalProvisioningOperation operation,
        ServiceClientOperationalProvisioningCheckpoint expected,
        ServiceClientOperationalProvisioningCheckpoint next,
        ServiceClientOperationalProvisioningState state,
        string eventType,
        long targetVersion,
        ServiceClientOperationalActor actor,
        CancellationToken ct)
    {
        var evidence = new ServiceClientOperationalProvisioningEvidence
        {
            Sequence = operation.EvidenceSequence + 1,
            EventType = eventType,
            OccurredAtUtc = _timeProvider.GetUtcNow(),
            ActorId = actor.UserId.ToString("D"),
            TargetOperationalVersion = targetVersion
        };
        if (await _operations.TryAdvanceAsync(operation.CommandId, operation.CommandFingerprint,
                expected, next, state, evidence, ct)) return;
        var current = await LoadExactOperationAsync(operation.CommandId, operation.CommandFingerprint, ct);
        if (current.Checkpoint < next)
            throw new ServiceClientOperationalRecoveryRequiredException("Operational checkpoint did not advance.");
    }

    private async Task<ServiceClientOperationalProvisioningOperation> LoadExactOperationAsync(
        Guid commandId, string fingerprint, CancellationToken ct)
    {
        var operation = await _operations.GetByCommandIdAsync(commandId, ct)
            ?? throw new ServiceClientOperationalRecoveryRequiredException("Operational command is missing.");
        if (!string.Equals(operation.CommandFingerprint, fingerprint, StringComparison.Ordinal))
            throw new ServiceClientOperationalConflictException("Operational command fingerprint drift.");
        return operation;
    }

    private async Task<ServiceClientOperationalProvisioningResult> ReadCompletedResultAsync(
        ServiceClientOperationalProvisioningOperation operation, bool replay, CancellationToken ct)
    {
        if (operation.Operation is ServiceClientOperationalOperations.EnableGrant
            or ServiceClientOperationalOperations.DisableGrant)
        {
            var identity = await RequireIdentityAsync(operation.TargetId, ct);
            var grant = await _grants.GetAsync(Required(operation.TenantId, "Tenant id"), operation.TargetId,
                Required(operation.Audience, "Audience"), ct)
                ?? throw new ServiceClientOperationalRecoveryRequiredException("Completed grant read-back is missing.");
            return GrantResult(operation.Operation, identity, grant, operation.CommandFingerprint, replay);
        }

        var target = await RequireIdentityAsync(operation.TargetId, ct);
        return IdentityResult(operation.Operation, target, operation.CommandFingerprint, replay,
            replay ? "not-reissued" : ServiceClientOperationalOperations.EmitsSecret(operation.Operation)
                ? "issued-once" : "not-applicable");
    }

    private async Task<ServiceClientIdentity> RequireIdentityAsync(Guid id, CancellationToken ct) =>
        await _identities.GetByIdAsync(id, ct)
        ?? throw new ServiceClientOperationalNotFoundException("Service client identity was not found.");

    private static void EnsurePurpose(
        ServiceClientIdentity identity, ServiceClientOperationalProvisioningRequest request)
    {
        if (!ServiceIdentityTokenAudiencePolicy.TryResolveIdentityAudience(identity.AllowedAudience, out var purpose)
            || !string.Equals(identity.ServiceName, Required(request.ServiceName, "Service name"), StringComparison.Ordinal)
            || !string.Equals(purpose, Required(request.Audience, "Audience"), StringComparison.Ordinal))
            throw new ServiceClientOperationalConflictException("Service client purpose drift.");
    }

    private static MutationOutcome ResolveMutation<T>(OperationalMutationResult<T> mutation, string? rawSecret)
        where T : class
    {
        var version = mutation.Entity switch
        {
            ServiceClientIdentity identity => identity.OperationalVersion,
            ServiceClientTenantGrant grant => grant.OperationalVersion,
            _ => 0
        };
        return mutation.Status switch
        {
            OperationalMutationStatus.Applied => new(true, version, rawSecret),
            OperationalMutationStatus.Replayed => new(false, version, null),
            OperationalMutationStatus.NotFound => throw new ServiceClientOperationalNotFoundException(
                "Operational target was not found."),
            _ => throw new ServiceClientOperationalConflictException("Operational version or uniqueness conflict.")
        };
    }

    private async Task TryMarkRecoveryRequiredAsync(
        ServiceClientOperationalProvisioningOperation operation, CancellationToken ct)
    {
        try
        {
            await _operations.TryMarkRecoveryRequiredAsync(
                operation.CommandId, operation.CommandFingerprint, operation.Checkpoint,
                ct.IsCancellationRequested ? CancellationToken.None : ct);
        }
        catch (ServiceIdentityPersistenceUnavailableException)
        {
            // The original failure is authoritative; recovery will re-read the durable checkpoint.
        }
    }

    private static void ValidateCommon(
        ServiceClientOperationalProvisioningRequest request, ServiceClientOperationalActor actor)
    {
        if (!ServiceClientOperationalOperations.IsSupported(request.Operation))
            throw new ServiceClientOperationalContractException("Unsupported operational command.");
        if (actor.UserId == Guid.Empty)
            throw new ServiceClientOperationalContractException("Authorized actor is required.");
    }

    private static string Fingerprint(
        ServiceClientOperationalProvisioningRequest request, ServiceClientOperationalActor actor)
    {
        var canonical = string.Join('\n',
            request.Operation,
            request.CommandId.ToString("D"),
            request.ExpectedOperationalVersion.ToString(CultureInfo.InvariantCulture),
            request.ServiceClientIdentityId?.ToString("D") ?? string.Empty,
            request.TenantId?.ToString("D") ?? string.Empty,
            request.ClientCode ?? string.Empty,
            request.ServiceName ?? string.Empty,
            request.Audience ?? string.Empty,
            actor.UserId.ToString("D"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private static string GenerateSecret() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(CredentialBytes))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string CredentialVersion(Guid commandId) => $"op-{commandId:N}";

    private static ServiceClientOperationalProvisioningResult IdentityResult(
        string operation, ServiceClientIdentity identity, string fingerprint, bool replay, string disposition) =>
        new(operation, identity.Id, null, identity.ClientCode, identity.ServiceName,
            identity.AllowedAudience ?? ServiceIdentityTokenAudiencePolicy.TrustedAuditSourceIngest,
            identity.OperationalVersion, fingerprint, replay, disposition);

    private static ServiceClientOperationalProvisioningResult GrantResult(
        string operation, ServiceClientIdentity identity, ServiceClientTenantGrant grant,
        string fingerprint, bool replay) =>
        new(operation, identity.Id, grant.TenantId, identity.ClientCode, identity.ServiceName,
            grant.Audience, grant.OperationalVersion, fingerprint, replay, "not-applicable");

    private static void ValidateBoundedText(string value, string name, int maxLength)
    {
        if (value.Length > maxLength || value.Any(char.IsControl))
            throw new ServiceClientOperationalContractException($"{name} is invalid.");
    }

    private static string Required(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
            throw new ServiceClientOperationalContractException($"{name} is required and must be canonical.");
        ValidateBoundedText(value, name, 256);
        return value;
    }

    private static Guid Required(Guid? value, string name) => value is { } id && id != Guid.Empty
        ? id
        : throw new ServiceClientOperationalContractException($"{name} is required.");

    private sealed record MutationOutcome(bool Applied, long OperationalVersion, string? RawSecret);
}
