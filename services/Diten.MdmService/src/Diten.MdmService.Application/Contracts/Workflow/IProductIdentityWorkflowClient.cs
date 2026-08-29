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
}
