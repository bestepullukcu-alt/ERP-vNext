using System.Globalization;
using System.Text.Json.Serialization;
using Diten.PlanningService.Domain.Features.DemandPlanning;

namespace Diten.PlanningService.Application.Features.DemandPlanning;

public sealed record RevisionScopeSeriesView(Guid SkuId, string WarehouseId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? BaseUomId = null);

public sealed record RevisionScopeSummaryView(int SelectedSeriesCount,
    int ExcludedSeriesCount, IReadOnlyList<RevisionScopeSeriesView> SelectedSeries);

public sealed record RevisionActorTraceView(Guid PreparedBy,
    DateTimeOffset PreparedAt, IReadOnlyList<Guid> SignificantEditorIds,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    Guid? ReviewedBy,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    DateTimeOffset? ReviewedAt,
    Guid PublishedBy, DateTimeOffset PublishedAt);

public sealed record RevisionInvalidationView(string ImpactCode, string Reason,
    string EvidenceReference, Guid ActorId, DateTimeOffset OccurredAt,
    int StateVersion);

public sealed record RevisionIntegrityView(string DefinitionId, string Digest);

public sealed record RevisionStatusView(string ContractVersion, Guid RevisionId,
    Guid PlanningCycleId, string PlanningPeriodKey, Guid TenantId,
    Guid LegalEntityId, string State, int StateVersion,
    string IntegrityState, DateTimeOffset ObservedAt,
    RevisionScopeSummaryView ScopeSummary,
    RevisionActorTraceView ActorTrace,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    RevisionInvalidationView? Invalidation);

public sealed record RevisionCalendarView(string CalendarId,
    string CalendarVersion, string TimeZoneId, DateOnly AsOfDate,
    DateOnly HorizonStart, DateOnly HorizonEnd,
    IReadOnlyList<PlanningWeek> Weeks);

public sealed record RevisionExcludedSeriesView(Guid SkuId,
    string WarehouseId, string Reason,
    Guid ExcludedBy, DateTimeOffset ExcludedAt);

public sealed record RevisionManifestView(string ContractVersion,
    Guid RevisionId, Guid PlanningCycleId, Guid TenantId,
    Guid LegalEntityId, string PlanningPeriodKey, string BaselineKind,
    string State, int StateVersion,
    RevisionScopeSummaryView ScopeSummary,
    RevisionActorTraceView ActorTrace,
    RevisionCalendarView Calendar,
    IReadOnlyList<RevisionScopeSeriesView> SelectedSeries,
    IReadOnlyList<RevisionExcludedSeriesView> ExcludedSeries,
    string DemandSetState, int ExpectedPartCount, int ExpectedRowCount,
    string OrderingDefinitionId, RevisionIntegrityView Integrity,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    RevisionInvalidationView? Invalidation);

public sealed record RevisionRowView(Guid SkuId, string WarehouseId,
    int WeekNumber, string ValueKind,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? GrossQuantityBaseUom, string Origin,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? ManualReason);

public sealed record RevisionRowPageView(string ContractVersion,
    Guid RevisionId, Guid PlanningCycleId, Guid TenantId,
    Guid LegalEntityId, int StateVersion,
    string OrderingDefinitionId, int ReturnedRowCount,
    int ExpectedRowCount, RevisionIntegrityView Integrity,
    IReadOnlyList<RevisionRowView> Items,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? NextCursor);

public sealed record InvalidatedAuditSnapshotView(string Warning,
    RevisionManifestView Manifest);

public sealed record InvalidatedAuditRowPageView(string Warning,
    string HistoricalState, RevisionRowPageView Page);

public static class RevisionReadViewMapper
{
    public const string OrderingDefinition = "MOD0188-SKU-WAREHOUSE-WEEK-1";

    public static RevisionStatusView Status(AuthoritativeRevisionStatus status,
        DateTimeOffset observedAt) => new("v2", status.RevisionId,
        status.PlanningCycleId, status.PlanningPeriodKey,
        status.TenantId, status.LegalEntityId, status.State.ToString(),
        status.StateVersion, "Unknown", observedAt,
        new RevisionScopeSummaryView(status.SelectedScope.Count,
            status.ExcludedSeriesCount,
            status.SelectedScope.Select(x =>
                new RevisionScopeSeriesView(x.SkuId, x.WarehouseId)).ToArray()),
        new RevisionActorTraceView(status.PreparedBy, status.PreparedAt,
            status.SignificantEditorIds, status.ReviewedBy,
            status.ReviewedAt, status.PublishedBy, status.PublishedAt),
        status.State == DemandRevisionState.Invalidated &&
        status.InvalidationReason is not null &&
        status.InvalidationImpactCode is not null &&
        status.InvalidationEvidenceReference is not null &&
        status.InvalidatedBy.HasValue && status.InvalidatedAt.HasValue
            ? new RevisionInvalidationView(status.InvalidationImpactCode,
                status.InvalidationReason,
                status.InvalidationEvidenceReference,
                status.InvalidatedBy.Value, status.InvalidatedAt.Value,
                status.StateVersion) : null);

    public static RevisionManifestView Manifest(InternalSnapshotManifest value,
        RevisionInvalidationView? invalidation = null)
    {
        var status = value.Status;
        var series = value.SelectedSeries.Select(x =>
            new RevisionScopeSeriesView(x.SkuId, x.WarehouseId,
                x.BaseUomId)).ToArray();
        var exclusions = value.ExcludedSeries.Select(x =>
            new RevisionExcludedSeriesView(x.SkuId, x.WarehouseId,
                x.Reason, x.ActorId, x.OccurredAt)).ToArray();
        return new RevisionManifestView("v2", status.RevisionId,
            status.PlanningCycleId, status.TenantId, status.LegalEntityId,
            status.PlanningPeriodKey, "Official", status.State.ToString(),
            status.StateVersion,
            new RevisionScopeSummaryView(series.Length, exclusions.Length,
                series),
            new RevisionActorTraceView(value.CreatedBy, value.CreatedAt,
                value.SignificantEditorIds, value.ReviewedBy,
                value.ReviewedAt, value.PublishedBy, value.PublishedAt),
            new RevisionCalendarView(value.CalendarId,
                value.CalendarVersion, value.TimeZoneId, value.AsOfDate,
                value.HorizonStart, value.HorizonEnd, value.Weeks),
            series, exclusions, series.Length == 0 ? "Empty" : "Populated",
            status.ExpectedPartCount, status.ExpectedRowCount,
            OrderingDefinition,
            new RevisionIntegrityView(status.ChecksumScheme, status.Checksum),
            invalidation);
    }

    public static RevisionRowPageView Page(InternalSnapshotStatus status,
        IReadOnlyList<InternalSnapshotRow> rows, string? nextCursor) =>
        new("v2", status.RevisionId, status.PlanningCycleId,
            status.TenantId, status.LegalEntityId, status.StateVersion,
            OrderingDefinition, rows.Count, status.ExpectedRowCount,
            new RevisionIntegrityView(status.ChecksumScheme, status.Checksum),
            rows.Select(x => new RevisionRowView(x.SkuId, x.WarehouseId,
                x.WeekNumber, x.ValueKind.ToString(),
                x.Quantity?.ToString(CultureInfo.InvariantCulture),
                x.Source.ToString(), x.Source == DraftWeekSource.Manual
                    ? x.ManualReason : null)).ToArray(), nextCursor);
}
