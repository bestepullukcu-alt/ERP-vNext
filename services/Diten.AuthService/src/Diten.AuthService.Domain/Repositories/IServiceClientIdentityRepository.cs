using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Domain.Repositories;

public interface IServiceClientIdentityRepository
{
    Task<ServiceClientIdentity?> GetByClientCodeAsync(string clientCode, CancellationToken ct);
    Task<ServiceClientIdentity?> GetByIdAsync(Guid id, CancellationToken ct) =>
        throw new NotSupportedException("Operational identity lookup is unavailable.");
    Task<OperationalMutationResult<ServiceClientIdentity>> RotateCredentialOperationalAsync(
        ServiceClientCredentialRotation mutation, CancellationToken ct) =>
        throw new NotSupportedException("Operational credential rotation is unavailable.");

    /// <summary>MOD-0033-FU02 §F — the document for a client code whether or not it is soft-deleted.</summary>
    Task<ServiceClientIdentity?> FindByClientCodeIncludingDeletedAsync(string clientCode, CancellationToken ct) =>
        throw new NotSupportedException("Service client registry lookup is unavailable.");

    /// <summary>
    /// MOD-0033-FU02 §F — inserts a declared identity; never updates. Returns false when the unique index (or the id)
    /// says another writer got there first, so two Auth instances starting together produce one identity.
    /// </summary>
    Task<bool> CreateIfAbsentAsync(ServiceClientIdentity identity, CancellationToken ct) =>
        throw new NotSupportedException("Service client registry creation is unavailable.");
}

/// <summary>All expected identity facts are part of the atomic predicate, including the current credential hash.</summary>
public sealed record ServiceClientCredentialRotation(
    Guid Id, string ClientCode, string ServiceName, string? PersistedAllowedAudience,
    long ExpectedOperationalVersion, string ExpectedActiveCredentialHash, string ExpectedActiveCredentialVersion,
    string CredentialHash, string CredentialVersion, DateTimeOffset PreviousValidUntilUtc,
    DateTimeOffset NowUtc, string ActorId, Guid CommandId, string CommandFingerprint);

public enum OperationalMutationStatus { Applied = 1, Replayed = 2, NotFound = 3, Conflict = 4 }
public sealed record OperationalMutationResult<T>(OperationalMutationStatus Status, T? Entity);
