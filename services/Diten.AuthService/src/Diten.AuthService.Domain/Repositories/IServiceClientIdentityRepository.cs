using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Domain.Repositories;

public interface IServiceClientIdentityRepository
{
    Task<ServiceClientIdentity?> GetByClientCodeAsync(string clientCode, CancellationToken ct);
    Task<ServiceClientIdentity?> GetByIdAsync(Guid id, CancellationToken ct) =>
        throw new NotSupportedException("Operational identity lookup is not supported by this repository.");
    Task<OperationalMutationResult<ServiceClientIdentity>> CreateOperationalAsync(
        ServiceClientIdentity identity, Guid commandId, string fingerprint, CancellationToken ct) =>
        throw new NotSupportedException("Operational identity creation is not supported by this repository.");
    Task<OperationalMutationResult<ServiceClientIdentity>> RotateCredentialOperationalAsync(
        Guid id, long expectedVersion, Guid commandId, string fingerprint,
        string previousCredentialHash, string previousCredentialVersion,
        string credentialHash, string credentialVersion, DateTimeOffset previousValidUntilUtc,
        DateTimeOffset nowUtc, string actorId, CancellationToken ct) =>
        throw new NotSupportedException("Operational credential rotation is not supported by this repository.");
    Task<OperationalMutationResult<ServiceClientIdentity>> RevokeOperationalAsync(
        Guid id, long expectedVersion, Guid commandId, string fingerprint,
        DateTimeOffset nowUtc, string actorId, CancellationToken ct) =>
        throw new NotSupportedException("Operational identity revocation is not supported by this repository.");
}

public enum OperationalMutationStatus
{
    Applied = 1,
    Replayed = 2,
    NotFound = 3,
    Conflict = 4
}

public sealed record OperationalMutationResult<T>(OperationalMutationStatus Status, T? Entity);
