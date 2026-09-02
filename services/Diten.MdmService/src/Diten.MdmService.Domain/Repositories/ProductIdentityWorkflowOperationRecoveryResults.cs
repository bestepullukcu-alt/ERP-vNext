using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Domain.Repositories;

public enum ProductIdentityWorkflowOperationFamily
{
    GlobalProduct = 1,
    FirstGsku = 2,
    Lsku = 3,
    FinishedGood = 4
}

public enum ProductIdentityWorkflowOperationRecoveryWriteStatus
{
    Applied = 1,
    ExactReplay = 2,
    NotFound = 3,
    Conflict = 4,
    Ineligible = 5
}

public abstract record ProductIdentityWorkflowOperationRecoveryScope
{
    private protected ProductIdentityWorkflowOperationRecoveryScope(
        ProductIdentityWorkflowOperationFamily family) => Family = family;

    public ProductIdentityWorkflowOperationFamily Family { get; }

    public abstract bool HasSameTarget(ProductIdentityWorkflowOperationRecoveryScope other);
}

public sealed record GlobalProductWorkflowRecoveryScope : ProductIdentityWorkflowOperationRecoveryScope
{
    public GlobalProductWorkflowRecoveryScope(Guid globalProductId, int globalProductVersion)
        : base(ProductIdentityWorkflowOperationFamily.GlobalProduct)
    {
        GlobalProductId = globalProductId;
        GlobalProductVersion = globalProductVersion;
    }

    public Guid GlobalProductId { get; }
    public int GlobalProductVersion { get; }

    public override bool HasSameTarget(ProductIdentityWorkflowOperationRecoveryScope other) =>
        other is GlobalProductWorkflowRecoveryScope candidate
        && candidate.GlobalProductId == GlobalProductId;
}

public sealed record FirstGskuWorkflowRecoveryScope : ProductIdentityWorkflowOperationRecoveryScope
{
    public FirstGskuWorkflowRecoveryScope(
        Guid globalProductId,
        Guid productDefinitionRevisionId,
        Guid gskuId,
        int gskuVersion,
        int productDefinitionRevisionVersion)
        : base(ProductIdentityWorkflowOperationFamily.FirstGsku)
    {
        GlobalProductId = globalProductId;
        ProductDefinitionRevisionId = productDefinitionRevisionId;
        GskuId = gskuId;
        GskuVersion = gskuVersion;
        ProductDefinitionRevisionVersion = productDefinitionRevisionVersion;
    }

    public Guid GlobalProductId { get; }
    public Guid ProductDefinitionRevisionId { get; }
    public Guid GskuId { get; }
    public int GskuVersion { get; }
    public int ProductDefinitionRevisionVersion { get; }

    public override bool HasSameTarget(ProductIdentityWorkflowOperationRecoveryScope other) =>
        other is FirstGskuWorkflowRecoveryScope candidate
        && candidate.GlobalProductId == GlobalProductId
        && candidate.ProductDefinitionRevisionId == ProductDefinitionRevisionId
        && candidate.GskuId == GskuId;
}

public sealed record LskuWorkflowRecoveryScope : ProductIdentityWorkflowOperationRecoveryScope
{
    public LskuWorkflowRecoveryScope(
        Guid lskuId,
        Guid gskuId,
        Guid productDefinitionRevisionId,
        string marketCode,
        int lskuVersion)
        : base(ProductIdentityWorkflowOperationFamily.Lsku)
    {
        LskuId = lskuId;
        GskuId = gskuId;
        ProductDefinitionRevisionId = productDefinitionRevisionId;
        MarketCode = marketCode;
        LskuVersion = lskuVersion;
    }

    public Guid LskuId { get; }
    public Guid GskuId { get; }
    public Guid ProductDefinitionRevisionId { get; }
    public string MarketCode { get; }
    public int LskuVersion { get; }

    public override bool HasSameTarget(ProductIdentityWorkflowOperationRecoveryScope other) =>
        other is LskuWorkflowRecoveryScope candidate
        && candidate.LskuId == LskuId
        && candidate.GskuId == GskuId
        && candidate.ProductDefinitionRevisionId == ProductDefinitionRevisionId
        && string.Equals(candidate.MarketCode, MarketCode, StringComparison.Ordinal);
}

public sealed record FinishedGoodWorkflowRecoveryScope : ProductIdentityWorkflowOperationRecoveryScope
{
    public FinishedGoodWorkflowRecoveryScope(
        Guid finishedGoodId,
        Guid gskuId,
        Guid productDefinitionRevisionId,
        int finishedGoodVersion)
        : base(ProductIdentityWorkflowOperationFamily.FinishedGood)
    {
        FinishedGoodId = finishedGoodId;
        GskuId = gskuId;
        ProductDefinitionRevisionId = productDefinitionRevisionId;
        FinishedGoodVersion = finishedGoodVersion;
    }

    public Guid FinishedGoodId { get; }
    public Guid GskuId { get; }
    public Guid ProductDefinitionRevisionId { get; }
    public int FinishedGoodVersion { get; }

    public override bool HasSameTarget(ProductIdentityWorkflowOperationRecoveryScope other) =>
        other is FinishedGoodWorkflowRecoveryScope candidate
        && candidate.FinishedGoodId == FinishedGoodId
        && candidate.GskuId == GskuId
        && candidate.ProductDefinitionRevisionId == ProductDefinitionRevisionId;
}

public sealed record ProductIdentityWorkflowOperationRecoveryCandidate(
    Guid OperationId,
    int OperationVersion,
    ProductIdentityWorkflowOperationRecoveryScope Scope,
    Guid OriginalMakerSubjectId,
    string StartIdempotencyKey,
    string OperationFingerprint,
    bool IsPrepared,
    bool HasPersistedWorkflowEvidence,
    ProductIdentityWorkflowRecoveryDisposition RecoveryDisposition,
    string? LeaseOwner,
    long? LeaseUntilUtcTicksV1,
    long LeaseGeneration);

public sealed record ProductIdentityWorkflowOperationRecoveryEvidence(
    ProductIdentityWorkflowRecoveryDisposition Disposition,
    Guid CommandId,
    Guid OperatorSubjectId,
    string ReasonCode,
    string? Comment,
    Guid WorkflowNotFoundEvidenceId,
    string WorkflowNotFoundEvidenceFingerprint,
    long WorkflowNotFoundObservedAtUtcTicksV1,
    long RecoveredAtUtcTicksV1);

public sealed record ProductIdentityWorkflowOperationRecoverySuccessor(
    Guid OperationId,
    string StartIdempotencyKey,
    string OperationFingerprint,
    Guid MakerSubjectId,
    ProductIdentityWorkflowOperationRecoveryScope Scope);

public sealed class ProductIdentityWorkflowOperationRecoveryMutation
{
    public ProductIdentityWorkflowOperationRecoveryMutation(
        Guid operationId,
        int expectedOperationVersion,
        ProductIdentityWorkflowOperationRecoveryScope expectedScope,
        Guid expectedOriginalMakerSubjectId,
        string expectedStartIdempotencyKey,
        string expectedOperationFingerprint,
        string? expectedLeaseOwner,
        long? expectedLeaseUntilUtcTicksV1,
        long expectedLeaseGeneration,
        ProductIdentityWorkflowOperationRecoveryEvidence evidence,
        ProductIdentityWorkflowOperationRecoverySuccessor? successor,
        IReadOnlyList<LocalAuditIntent> auditIntents)
    {
        ArgumentNullException.ThrowIfNull(expectedScope);
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(auditIntents);

        if (successor is not null)
        {
            ArgumentNullException.ThrowIfNull(successor.Scope);
            if (!expectedScope.HasSameTarget(successor.Scope))
            {
                throw new ArgumentException(
                    "The successor must use the exact same workflow-operation family and target tuple.",
                    nameof(successor));
            }
        }

        OperationId = operationId;
        ExpectedOperationVersion = expectedOperationVersion;
        ExpectedScope = expectedScope;
        ExpectedOriginalMakerSubjectId = expectedOriginalMakerSubjectId;
        ExpectedStartIdempotencyKey = expectedStartIdempotencyKey;
        ExpectedOperationFingerprint = expectedOperationFingerprint;
        ExpectedLeaseOwner = expectedLeaseOwner;
        ExpectedLeaseUntilUtcTicksV1 = expectedLeaseUntilUtcTicksV1;
        ExpectedLeaseGeneration = expectedLeaseGeneration;
        Evidence = evidence;
        Successor = successor;
        AuditIntents = auditIntents.ToArray();
    }

    public Guid OperationId { get; }
    public int ExpectedOperationVersion { get; }
    public ProductIdentityWorkflowOperationRecoveryScope ExpectedScope { get; }
    public Guid ExpectedOriginalMakerSubjectId { get; }
    public string ExpectedStartIdempotencyKey { get; }
    public string ExpectedOperationFingerprint { get; }
    public string? ExpectedLeaseOwner { get; }
    public long? ExpectedLeaseUntilUtcTicksV1 { get; }
    public long ExpectedLeaseGeneration { get; }
    public ProductIdentityWorkflowOperationRecoveryEvidence Evidence { get; }
    public ProductIdentityWorkflowOperationRecoverySuccessor? Successor { get; }
    public IReadOnlyList<LocalAuditIntent> AuditIntents { get; }
}

public sealed record ProductIdentityWorkflowOperationRecoveryWriteResult(
    ProductIdentityWorkflowOperationRecoveryWriteStatus Status,
    Guid OperationId,
    ProductIdentityWorkflowOperationRecoveryScope? Scope,
    ProductIdentityWorkflowRecoveryDisposition? Disposition,
    int? OperationVersion,
    ProductIdentityWorkflowOperationRecoverySuccessor? Successor,
    string? ErrorCode = null)
{
    public bool Succeeded => Status is ProductIdentityWorkflowOperationRecoveryWriteStatus.Applied
        or ProductIdentityWorkflowOperationRecoveryWriteStatus.ExactReplay;

    public bool IsReplay => Status == ProductIdentityWorkflowOperationRecoveryWriteStatus.ExactReplay;
}
