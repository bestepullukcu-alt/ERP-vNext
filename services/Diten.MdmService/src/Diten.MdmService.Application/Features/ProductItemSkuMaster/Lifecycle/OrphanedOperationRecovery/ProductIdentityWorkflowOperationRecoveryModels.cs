using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.OrphanedOperationRecovery;

public enum ProductIdentityWorkflowOperationRecoveryAction
{
    Abandon = 1,
    Supersede = 2
}

/// <summary>
/// Carries the route-discovered family's optimistic-concurrency versions.
/// PrimaryEntityVersion is the selected family's main entity version and is the GSKU Version for FirstGsku.
/// ProductDefinitionRevisionVersion is required only for FirstGsku and is forbidden for every other family.
/// </summary>
public sealed record ProductIdentityWorkflowTargetVersions(
    int PrimaryEntityVersion,
    int? ProductDefinitionRevisionVersion = null);

public sealed record ProductIdentityWorkflowOperationRecoveryRequest(
    ProductIdentityWorkflowOperationRecoveryAction Action,
    int ExpectedOperationVersion,
    ProductIdentityWorkflowTargetVersions ExpectedTargetVersion,
    string ReasonCode,
    string? Comment);

public sealed record ProductIdentityWorkflowOperationRecoverySuccessorResult(
    Guid OperationId,
    ProductIdentityWorkflowTargetVersions TargetVersion);

public sealed record ProductIdentityWorkflowOperationRecoveryResult(
    Guid OperationId,
    ProductIdentityWorkflowOperationFamily OperationFamily,
    ProductIdentityWorkflowRecoveryDisposition RecoveryDisposition,
    int OperationVersion,
    ProductIdentityWorkflowTargetVersions TargetVersion,
    ProductIdentityWorkflowOperationRecoverySuccessorResult? Successor);
