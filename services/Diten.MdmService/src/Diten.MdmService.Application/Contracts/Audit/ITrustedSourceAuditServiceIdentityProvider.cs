namespace Diten.MdmService.Application.Contracts.Audit;

public interface ITrustedSourceAuditServiceIdentityProvider
{
    Task<TrustedSourceAuditServiceIdentity> GetAsync(
        Guid tenantId,
        string audience,
        bool forceRefresh,
        CancellationToken cancellationToken = default);
}
public sealed class TrustedSourceAuditServiceIdentityException : Exception
{
    public TrustedSourceAuditServiceIdentityException(string errorCode, bool isRetryable, Exception? inner = null)
        : base(errorCode, inner)
    {
        ErrorCode = errorCode;
        IsRetryable = isRetryable;
    }

    public string ErrorCode { get; }
    public bool IsRetryable { get; }
}
