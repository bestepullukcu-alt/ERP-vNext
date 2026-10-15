namespace Diten.MdmService.Application.Contracts.Workflow;

public interface IProductIdentityWorkflowServiceIdentityProvider
{
    Task<ProductIdentityWorkflowServiceIdentity> GetAsync(
        Guid tenantId,
        bool forceRefresh,
        CancellationToken cancellationToken = default);
}

public sealed record ProductIdentityWorkflowServiceIdentity(string AccessToken, DateTimeOffset ExpiresAtUtc);

public sealed class ProductIdentityWorkflowServiceIdentityException : Exception
{
    public ProductIdentityWorkflowServiceIdentityException(string errorCode, bool isRetryable, Exception? inner = null)
        : base(errorCode, inner)
    {
        ErrorCode = errorCode;
        IsRetryable = isRetryable;
    }

    public string ErrorCode { get; }
    public bool IsRetryable { get; }
}
