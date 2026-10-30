namespace Diten.SupplyChainService.Domain.Features.SupplierPerformance;
public sealed record EvaluationReceipt(string Fingerprint, int StatusCode, string Body);
public enum EvaluationCommitOutcome { Committed, Clash, Unknown }
public sealed record EvaluationFilter(string? SupplierId, EvaluationStatus? Status, string? PeriodFrom, string? PeriodTo);
public sealed record ScorecardFilter(string? SupplierId, string? PeriodFrom, string? PeriodTo, string? RiskLevel);
public interface IEvaluationRepository
{
    /// <summary>§0.5 stage 5: the receipt of (Tenant, LE, createSupplierEvaluation, create, key), compared ordinally.</summary>
    /// <param name="afterClash">Read at the level the clash was raised (local): a winner's commit can refuse this write before
    /// the majority snapshot shows its receipt.</param>
    Task<EvaluationReceipt?> FindCreateReceiptAsync(SupplierPerformanceScope scope, string key, CancellationToken ct, bool afterClash = false);
    Task<bool> ActivePeriodExistsAsync(SupplierPerformanceScope scope, string supplierId, string periodStart, string periodEnd, CancellationToken ct);
    /// <summary>§0.5 stage 8: evaluation + receipt + central audit in one transaction. Clash = a unique index refused it; Unknown = the commit could not be proven either way.</summary>
    Task<EvaluationCommitOutcome> CreateAsync(SupplierPerformanceScope scope, string key, string fingerprint, SupplierEvaluation evaluation, string responseBody, CancellationToken ct);
    Task<SupplierEvaluation?> GetAsync(SupplierPerformanceScope scope, Guid evaluationId, CancellationToken ct);
    Task<(IReadOnlyList<SupplierEvaluation> Items, long Total)> ListAsync(SupplierPerformanceScope scope, EvaluationFilter filter, int page, int pageSize, CancellationToken ct);
    Task<SupplierEvaluation?> GetPublishedAsync(SupplierPerformanceScope scope, Guid scorecardId, CancellationToken ct);
    Task<IReadOnlyList<SupplierEvaluation>> ListPublishedAsync(SupplierPerformanceScope scope, ScorecardFilter filter, CancellationToken ct);
}
