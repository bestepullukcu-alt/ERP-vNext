namespace Diten.Platform.Application.Authorization;

public interface IOrgDataScopeCandidateResolver
{
    Task<OrgDataScopeCandidateSet> ResolveAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);
}

public interface IOrgDataScopeCandidateAvailabilityClassifier
{
    bool IsUnavailable(Exception exception);
}

public sealed class OrgDataScopeCandidateUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public sealed class OrgDataScopeCandidateContractException(string message) : Exception(message);
