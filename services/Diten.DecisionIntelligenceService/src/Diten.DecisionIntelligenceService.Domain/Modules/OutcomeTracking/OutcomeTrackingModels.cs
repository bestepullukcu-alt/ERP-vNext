using Diten.DecisionIntelligenceService.Domain.Modules.OutcomeTracking.Common;

namespace Diten.DecisionIntelligenceService.Domain.Modules.OutcomeTracking;

public enum OutcomeKind { Quantitative, Qualitative }
public enum OutcomeLifecycleState { Active, Retired }
public enum UnitKind { Count, Percentage, Currency, Days, Score }
public enum CorrectionReason { DataCorrection, SourceRestatement, MethodologyCorrection }
public enum ObservationStatus { NoObservation, Current, Stale }
public enum ReferenceMode { HistoricalResolve, NewReferenceEligibility, CurrentSelectionEligibility }
public sealed class DomainConflictException(string message = "Conflict") : Exception(message);

public sealed class Outcome : OutcomeTrackingEntityBase
{
    public Outcome(Guid id, Guid tenantId, string code, string title, OutcomeKind kind, Guid actorId) : base(id, tenantId, actorId)
    { Code = Required(code, 64); Title = Required(title, 200); Kind = kind; }
    public string Code { get; }
    public string Title { get; }
    public OutcomeKind Kind { get; }
    public OutcomeLifecycleState Lifecycle { get; private set; }
    public int CurrentVersionNumber { get; private set; }
    public void Publish(OutcomeVersion version) { if (Lifecycle != OutcomeLifecycleState.Active || version.TenantId != TenantId || version.OutcomeId != Id || version.OutcomeVersionNumber != CurrentVersionNumber + 1) throw new DomainConflictException(); CurrentVersionNumber++; Version++; }
    public void Retire() { if (Lifecycle != OutcomeLifecycleState.Active) throw new DomainConflictException(); Lifecycle = OutcomeLifecycleState.Retired; Version++; }
    private static string Required(string value, int max) { value = value.Trim().Normalize(); if (value.Length is 0 || value.Length > max) throw new ArgumentException(); return value; }
}

public sealed record MetricReferenceV1(string ContractName, string ContractVersion, Guid MetricId, Guid MetricVersionId, int MetricVersionNumber)
{ public const string Name = "decision-intelligence.outcome-metric-reference"; public const string Version = "1.0"; public MetricReferenceV1 Validate() => ContractName == Name && ContractVersion == Version && MetricId != Guid.Empty && MetricVersionId != Guid.Empty && MetricVersionNumber > 0 ? this : throw new ArgumentException(); }
public sealed record ScaleReferenceV1(string ContractName, string ContractVersion, Guid ScaleId, Guid ScaleVersionId, int ScaleVersionNumber)
{ public const string Name = "decision-intelligence.qualitative-scale-reference"; public const string Version = "1.0"; public ScaleReferenceV1 Validate() => ContractName == Name && ContractVersion == Version && ScaleId != Guid.Empty && ScaleVersionId != Guid.Empty && ScaleVersionNumber > 0 ? this : throw new ArgumentException(); }

public sealed class OutcomeVersion : OutcomeTrackingEntityBase
{
    public OutcomeVersion(Guid id, Guid tenantId, Guid outcomeId, int number, OutcomeKind kind, MetricReferenceV1? metric, ScaleReferenceV1? scale, Guid actorId) : base(id, tenantId, actorId)
    { if (outcomeId == Guid.Empty || number < 1 || (kind == OutcomeKind.Quantitative) != (metric is not null) || (kind == OutcomeKind.Qualitative) != (scale is not null)) throw new ArgumentException(); Metric = metric?.Validate(); Scale = scale?.Validate(); OutcomeId = outcomeId; OutcomeVersionNumber = number; Kind = kind; }
    public Guid OutcomeId { get; }
    public int OutcomeVersionNumber { get; }
    public OutcomeKind Kind { get; }
    public MetricReferenceV1? Metric { get; }
    public ScaleReferenceV1? Scale { get; }
}

public sealed record OutcomeMetric(Guid Id, Guid TenantId, string Code);
public sealed record OutcomeMetricVersion(Guid Id, Guid TenantId, Guid MetricId, int Number, UnitKind Unit, string? Currency, decimal Min, decimal Max, int FreshnessDays)
{
    public OutcomeMetricVersion Validate() { const decimal hard = 1_000_000_000_000_000m; if (Id == Guid.Empty || TenantId == Guid.Empty || MetricId == Guid.Empty || Number < 1 || FreshnessDays is < 1 or > 366 || Min > Max || Min < -hard || Max > hard || DecimalScale(Min) > 4 || DecimalScale(Max) > 4 || (Unit == UnitKind.Currency) != (Currency is not null)) throw new ArgumentException(); if (Unit == UnitKind.Percentage && (Min < 0 || Max > 100) || Unit is UnitKind.Count or UnitKind.Days && Min < 0 || Unit == UnitKind.Score && (Min < 0 || Max > 100)) throw new ArgumentException(); return this; }
    public void ValidateValue(decimal value) { Validate(); if (DecimalScale(value) > 4 || value < Min || value > Max) throw new ArgumentException(); }
    private static int DecimalScale(decimal value) => (decimal.GetBits(value)[3] >> 16) & 0x7F;
}

public sealed record QualitativeScale(Guid Id, Guid TenantId, string Code);
public sealed record QualitativeLevel(string Key, int Ordinal, string Label);
public sealed class QualitativeScaleVersion
{
    public QualitativeScaleVersion(Guid id, Guid tenantId, Guid scaleId, int number, IReadOnlyList<QualitativeLevel> levels) { if (id == Guid.Empty || tenantId == Guid.Empty || scaleId == Guid.Empty || number < 1 || levels.Count is < 2 or > 20 || levels.Any(x => string.IsNullOrWhiteSpace(x.Key) || x.Key != x.Key.Trim().Normalize() || x.Ordinal < 1 || string.IsNullOrWhiteSpace(x.Label)) || levels.Select(x => x.Key).Distinct(StringComparer.Ordinal).Count() != levels.Count || levels.Select(x => x.Ordinal).Distinct().Count() != levels.Count) throw new ArgumentException(); Id = id; TenantId = tenantId; ScaleId = scaleId; Number = number; Levels = levels.ToArray(); }
    public Guid Id { get; } public Guid TenantId { get; } public Guid ScaleId { get; } public int Number { get; } public IReadOnlyList<QualitativeLevel> Levels { get; }
    public void ValidateLevel(string key) { if (!Levels.Any(x => string.Equals(x.Key, key, StringComparison.Ordinal))) throw new ArgumentException(); }
}

public sealed record MeasurementPeriod(DateOnly Start, DateOnly End)
{
    public MeasurementPeriod Validate(DateOnly today) { if (Start >= End || End.DayNumber - Start.DayNumber > 366 || End > today.AddDays(1) || Start < today.AddYears(-10)) throw new ArgumentException(); return this; }
    public bool Overlaps(MeasurementPeriod other) => Start < other.End && other.Start < End;
}

public sealed class OutcomeMeasurement : OutcomeTrackingEntityBase
{
    public OutcomeMeasurement(Guid id, Guid tenantId, Guid outcomeId, Guid versionId, OutcomeKind kind, MeasurementPeriod period, decimal? value, string? level, Guid? supersedes, CorrectionReason? reason, Guid actorId) : base(id, tenantId, actorId)
    { if (outcomeId == Guid.Empty || versionId == Guid.Empty || (kind == OutcomeKind.Quantitative) != (value is not null) || (kind == OutcomeKind.Qualitative) != (level is not null) || (supersedes is null) != (reason is null) || supersedes == id) throw new ArgumentException(); OutcomeId = outcomeId; OutcomeVersionId = versionId; Kind = kind; Period = period; Value = value; Level = level; SupersedesMeasurementId = supersedes; CorrectionReason = reason; }
    public Guid OutcomeId { get; } public Guid OutcomeVersionId { get; } public OutcomeKind Kind { get; } public MeasurementPeriod Period { get; } public decimal? Value { get; } public string? Level { get; } public Guid? SupersedesMeasurementId { get; } public CorrectionReason? CorrectionReason { get; }
}

public sealed record DecisionOutcomeLink(Guid Id, Guid TenantId, Guid OutcomeId, Guid OutcomeVersionId, Guid DecisionId, Guid DecisionVersionId);
public sealed record OutcomeReferenceV1(string ContractName, string ContractVersion, Guid OutcomeId, Guid OutcomeVersionId, int OutcomeVersionNumber)
{ public const string Name = "diten.decision-intelligence.outcome-reference"; public const string Version = "1.0"; public OutcomeReferenceV1 Validate() => ContractName == Name && ContractVersion == Version && OutcomeId != Guid.Empty && OutcomeVersionId != Guid.Empty && OutcomeVersionNumber > 0 ? this : throw new ArgumentException(); }
public sealed record BenefitCommitmentOutcomeReferenceV1(string ContractName, string ContractVersion, Guid BenefitCommitmentId, OutcomeReferenceV1 OutcomeReference)
{ public const string Name = "ppm.benefit-commitment-outcome-reference"; public const string Version = "1.0"; public BenefitCommitmentOutcomeReferenceV1 Validate() { if (ContractName != Name || ContractVersion != Version || BenefitCommitmentId == Guid.Empty) throw new ArgumentException(); OutcomeReference.Validate(); return this; } }
public static class OutcomeReferencePolicy { public static int Evaluate(ReferenceMode mode, bool visible, bool retired) => !visible ? 404 : mode switch { ReferenceMode.HistoricalResolve => 200, ReferenceMode.NewReferenceEligibility => retired ? 409 : 200, ReferenceMode.CurrentSelectionEligibility => 400, _ => 400 }; }
