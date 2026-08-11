using Diten.PvgService.Domain.SignalManagement;

namespace Diten.PvgService.Application.SignalManagement;

public sealed class InMemorySignalManagementService
{
    private readonly Dictionary<string, StoredSignalContract> _contracts = new(StringComparer.Ordinal);
    private int _nextReferenceNumber;

    public int StoredContractCount => _contracts.Count;

    public SignalManagementInMemoryResult CreateSignalHypothesisContract(CreateSignalHypothesisContractCommand command)
    {
        var guardResult = EvaluateSafely(
            SignalManagementOperation.CreateSignalHypothesisContract,
            () => SignalManagementContractGuard.Evaluate(command));

        if (!guardResult.IsAllowed)
        {
            return SignalManagementInMemoryResult.FromSafeResult(guardResult);
        }

        var token = CreateOpaqueContractReference();
        var stored = new StoredSignalContract(
            command.ServerTenantContext.TenantContextReference,
            token,
            SignalReviewDecisionStatus.Draft,
            false,
            false);

        _contracts[token] = stored;

        return SignalManagementInMemoryResult.Allowed(
            SignalManagementOperation.CreateSignalHypothesisContract,
            ToMetadata(stored));
    }

    public SignalManagementInMemoryResult AttachMetricDataProductCohortReference(
        AttachSignalMetricDataProductReferenceCommand command)
    {
        var guardResult = EvaluateSafely(
            SignalManagementOperation.AttachSignalMetricDataProductReference,
            () => SignalManagementContractGuard.Evaluate(command));

        if (!guardResult.IsAllowed)
        {
            return SignalManagementInMemoryResult.FromSafeResult(guardResult);
        }

        if (!TryGetSameTenantContract(
                command.SignalHypothesisReferenceToken,
                command.ServerTenantContext,
                out var stored))
        {
            return SignalManagementInMemoryResult.Blocked(
                SignalManagementOperation.AttachSignalMetricDataProductReference,
                SignalManagementReasonCode.NotFoundOrUnavailable);
        }

        var updated = stored with
        {
            HasMetricReference = true,
            HasDataProductCohortReference = true
        };
        _contracts[updated.SignalHypothesisReferenceToken] = updated;

        return SignalManagementInMemoryResult.Allowed(
            SignalManagementOperation.AttachSignalMetricDataProductReference,
            ToMetadata(updated));
    }

    public SignalManagementInMemoryResult MarkReviewDecisionContract(MarkSignalReviewDecisionContractCommand command)
    {
        var guardResult = EvaluateSafely(
            SignalManagementOperation.MarkSignalReviewDecisionContract,
            () => SignalManagementContractGuard.Evaluate(command));

        if (!guardResult.IsAllowed)
        {
            return SignalManagementInMemoryResult.FromSafeResult(guardResult);
        }

        if (!TryGetSameTenantContract(
                command.SignalHypothesisReferenceToken,
                command.ServerTenantContext,
                out var stored))
        {
            return SignalManagementInMemoryResult.Blocked(
                SignalManagementOperation.MarkSignalReviewDecisionContract,
                SignalManagementReasonCode.NotFoundOrUnavailable);
        }

        var updated = stored with { ReviewDecisionStatus = command.ReviewDecisionStatus };
        _contracts[updated.SignalHypothesisReferenceToken] = updated;

        return SignalManagementInMemoryResult.Allowed(
            SignalManagementOperation.MarkSignalReviewDecisionContract,
            ToMetadata(updated));
    }

    public SignalManagementInMemoryResult GetByIdMetadata(GetSignalContractMetadataByIdQuery query)
    {
        var guardResult = EvaluateSafely(
            SignalManagementOperation.GetById,
            () => SignalManagementContractGuard.Evaluate(query));

        if (!guardResult.IsAllowed)
        {
            return SignalManagementInMemoryResult.FromSafeResult(guardResult);
        }

        return TryGetSameTenantContract(query.SignalHypothesisReferenceToken, query.ServerTenantContext, out var stored)
            ? SignalManagementInMemoryResult.Allowed(SignalManagementOperation.GetById, ToMetadata(stored))
            : SignalManagementInMemoryResult.Blocked(
                SignalManagementOperation.GetById,
                SignalManagementReasonCode.NotFoundOrUnavailable);
    }

    public SignalManagementInMemoryResult ListMetadata(GetSignalContractMetadataListQuery query)
    {
        var guardResult = EvaluateSafely(
            SignalManagementOperation.List,
            () => SignalManagementContractGuard.Evaluate(query));

        if (!guardResult.IsAllowed)
        {
            return SignalManagementInMemoryResult.FromSafeResult(guardResult);
        }

        var tenantReference = query.ServerTenantContext.TenantContextReference;
        var visibleContracts = _contracts.Values
            .Where(stored => string.Equals(stored.ServerTenantContextReference, tenantReference, StringComparison.Ordinal))
            .OrderBy(stored => stored.SignalHypothesisReferenceToken, StringComparer.Ordinal)
            .Select(ToMetadata)
            .ToArray();

        return SignalManagementInMemoryResult.Allowed(SignalManagementOperation.List, visibleContracts);
    }

    private bool TryGetSameTenantContract(
        string signalHypothesisReferenceToken,
        SignalManagementServerTenantContext serverTenantContext,
        out StoredSignalContract stored)
    {
        if (_contracts.TryGetValue(signalHypothesisReferenceToken, out var candidate) &&
            string.Equals(
                candidate.ServerTenantContextReference,
                serverTenantContext.TenantContextReference,
                StringComparison.Ordinal))
        {
            stored = candidate;
            return true;
        }

        stored = default;
        return false;
    }

    private string CreateOpaqueContractReference()
    {
        _nextReferenceNumber++;
        return $"signal-contract-{_nextReferenceNumber:D6}";
    }

    private static SignalManagementSafeResult EvaluateSafely(
        SignalManagementOperation operation,
        Func<SignalManagementSafeResult> evaluate)
    {
        try
        {
            return evaluate();
        }
        catch
        {
            return SignalManagementSafeResult.Blocked(operation, SignalManagementReasonCode.InvalidRequest);
        }
    }

    private static SignalContractMetadata ToMetadata(StoredSignalContract stored) =>
        new(
            stored.SignalHypothesisReferenceToken,
            stored.ReviewDecisionStatus,
            stored.HasMetricReference,
            stored.HasDataProductCohortReference);

    private readonly record struct StoredSignalContract(
        string ServerTenantContextReference,
        string SignalHypothesisReferenceToken,
        SignalReviewDecisionStatus ReviewDecisionStatus,
        bool HasMetricReference,
        bool HasDataProductCohortReference);
}

public sealed record SignalContractMetadata(
    string SignalHypothesisReferenceToken,
    SignalReviewDecisionStatus ReviewDecisionStatus,
    bool HasMetricReference,
    bool HasDataProductCohortReference);

public sealed record SignalManagementInMemoryResult(
    bool IsAllowed,
    SignalManagementReasonCode ReasonCode,
    IReadOnlyDictionary<string, string> Metadata,
    SignalContractMetadata? Contract,
    IReadOnlyCollection<SignalContractMetadata> Contracts)
{
    public static SignalManagementInMemoryResult FromSafeResult(SignalManagementSafeResult safeResult) =>
        new(safeResult.IsAllowed, safeResult.ReasonCode, safeResult.Metadata, null, Array.Empty<SignalContractMetadata>());

    public static SignalManagementInMemoryResult Allowed(
        SignalManagementOperation operation,
        SignalContractMetadata contract) =>
        new(
            true,
            SignalManagementReasonCode.None,
            SafeMetadata(operation, "Allowed"),
            contract,
            Array.Empty<SignalContractMetadata>());

    public static SignalManagementInMemoryResult Allowed(
        SignalManagementOperation operation,
        IReadOnlyCollection<SignalContractMetadata> contracts) =>
        new(
            true,
            SignalManagementReasonCode.None,
            SafeMetadata(operation, "Allowed"),
            null,
            contracts);

    public static SignalManagementInMemoryResult Blocked(
        SignalManagementOperation operation,
        SignalManagementReasonCode reasonCode) =>
        new(false, reasonCode, SafeMetadata(operation, "Blocked"), null, Array.Empty<SignalContractMetadata>());

    private static IReadOnlyDictionary<string, string> SafeMetadata(SignalManagementOperation operation, string result) =>
        new Dictionary<string, string>
        {
            ["module"] = "MOD-0234",
            ["operation"] = operation.ToString(),
            ["result"] = result
        };
}
