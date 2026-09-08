namespace Diten.MdmService.Application.Contracts.Workflow;

public interface IProductIdentityWorkflowClient
{
    Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>> StartAsync(
        Guid tenantId,
        ProductIdentityWorkflowStartRequest request,
        string delegatedUserToken,
        CancellationToken cancellationToken = default);

    Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>> GetStartResultAsync(
        Guid tenantId,
        ProductIdentityWorkflowStartResultRequest request,
        CancellationToken cancellationToken = default);

    Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowTerminalEvidence>> GetTerminalEvidenceAsync(
        Guid tenantId,
        ProductIdentityWorkflowTerminalEvidenceRequest request,
        CancellationToken cancellationToken = default);

    Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowCancellationPreflight>>
        GetCancellationPreflightAsync(
            Guid tenantId,
            ProductIdentityWorkflowCancellationPreflightRequest request,
            CancellationToken cancellationToken = default) =>
        Task.FromResult(ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowCancellationPreflight>.Fail(
            ProductIdentityWorkflowTransportOutcome.Invalid,
            "PRODUCT_IDENTITY_WORKFLOW_CANCELLATION_UNSUPPORTED"));

    Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowCancellationEvidence>> CancelAsync(
        Guid tenantId,
        ProductIdentityWorkflowCancellationRequest request,
        string delegatedUserToken,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowCancellationEvidence>.Fail(
            ProductIdentityWorkflowTransportOutcome.Invalid,
            "PRODUCT_IDENTITY_WORKFLOW_CANCELLATION_UNSUPPORTED"));
}
