using Vfp = Diten.CrmService.Domain.Entities.VisitFrequencyPolicy;

namespace Diten.CrmService.Application.Features.VisitFrequencyPolicy.Analysis;

/// <summary>WP-FREQ-DET-A — the ETKİ (impact) count for a policy's target: how many entities the target resolves to.
/// <para><see cref="Count"/> is <c>null</c> whenever <see cref="Computable"/> is false — a count is never fabricated as
/// 0. <see cref="Note"/> explains an uncomputable count (segment cap exceeded, segment missing, or a target type with
/// no ready counting path).</para></summary>
public sealed record VisitFrequencyTargetImpact(int? Count, bool Computable, string? Note)
{
    public static VisitFrequencyTargetImpact Countable(int count) => new(count, true, null);

    /// <summary>A real, computable count that still carries a caveat — e.g. a DRAFT segment counted through a preview
    /// wrapper, whose reach reflects today's data rather than a frozen, in-effect definition.</summary>
    public static VisitFrequencyTargetImpact CountableWithNote(int count, string note) => new(count, true, note);

    public static VisitFrequencyTargetImpact NotCountable(string note) => new(null, false, note);
}

/// <summary>
/// WP-FREQ-DET-A read-only seam that counts a frequency policy target's reach by REUSING the existing readers
/// (segment membership resolver, territory coverage resolver, campaign-target repository) — it introduces no new
/// matching engine and performs no writes. Isolating the per-type counting behind this interface keeps the analysis
/// handler's projection/normalisation logic unit-testable and mirrors the FU03 resolver seam pattern.
/// </summary>
public interface IVisitFrequencyTargetImpactCounter
{
    Task<VisitFrequencyTargetImpact> CountAsync(Guid tenantId, Vfp policy, DateTimeOffset at, CancellationToken cancellationToken);
}
