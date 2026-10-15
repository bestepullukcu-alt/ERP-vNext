namespace Diten.MdmService.Application.Contracts.ReferenceData;

public interface IWorkflowVerifiedMarketServiceIdentityProvider
{
    Task<WorkflowVerifiedMarketServiceIdentity> GetAsync(
        Guid tenantId,
        bool forceRefresh,
        CancellationToken cancellationToken = default);
}

public sealed record WorkflowVerifiedMarketServiceIdentity(
    string AccessToken,
    DateTimeOffset ExpiresAtUtc);

public sealed class WorkflowVerifiedMarketServiceIdentityException : Exception
{
    public WorkflowVerifiedMarketServiceIdentityException(
        string errorCode,
        bool isRetryable,
        Exception? innerException = null)
        : base(errorCode, innerException)
    {
        ErrorCode = errorCode;
        IsRetryable = isRetryable;
    }

    public string ErrorCode { get; }
    public bool IsRetryable { get; }
}
