using Diten.PlanningService.Domain.Features.DemandPlanning;
using System.Text.Json.Serialization;

namespace Diten.PlanningService.Application.Features.DemandPlanning.ManualDrafts;

// Production remains closed until Platform scope and MDM/LOCATION reference
// contracts are approved. A client-supplied SKU, Warehouse or UoM is never
// itself a verified reference.
public interface IManualDraftAuthority : ILegalEntityAssignmentAuthority
{
    Task<IReadOnlyList<VerifiedDraftSeriesReference>?> VerifyCreateSeriesAsync(
        Guid tenantId, Guid actorId, Guid legalEntityId,
        IReadOnlyList<DraftSeriesKey> requested, CancellationToken cancellationToken);

    Task<bool?> CanAccessAsync(Guid tenantId, Guid actorId, Guid legalEntityId,
        IReadOnlyList<DraftSeriesKey> series, CancellationToken cancellationToken);
}

public sealed record DraftSeriesKey(Guid SkuId, string WarehouseId);
public sealed record VerifiedDraftSeriesReference(
    Guid SkuId, string WarehouseId, string BaseUomId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ManualDraftWeekValue(int Number, DraftWeekValueKind ValueKind,
    decimal? Quantity);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ManualDraftSeriesInput(Guid SkuId, string WarehouseId,
    IReadOnlyList<ManualDraftWeekValue> Weeks);

public sealed record ManualDraftWeekView(int Number, DateOnly WeekStart,
    DateOnly WeekEnd, DraftWeekValueKind ValueKind, decimal? Quantity,
    DraftWeekSource Source, string ManualReason, Guid ManualActorId,
    DateTimeOffset ManualAt);
public sealed record ManualDraftSeriesView(Guid SkuId, string WarehouseId,
    string BaseUomId, IReadOnlyList<ManualDraftWeekView> Weeks,
    DraftSeriesExclusion? Exclusion);
public sealed record ManualDraftView(Guid RevisionId, Guid PlanningCycleId,
    Guid TenantId, Guid LegalEntityId, string PlanningPeriodKey,
    DateOnly AsOfDate, string CalendarId, string CalendarVersion,
    string TimeZoneId, DateOnly HorizonStart, DateOnly HorizonEnd,
    DemandRevisionState State, int ContentVersion, int StateVersion,
    Guid CreatedBy, DateTimeOffset CreatedAt, string CreationReason,
    Guid? ReviewedBy, DateTimeOffset? ReviewedAt, string? ReviewReason,
    IReadOnlyList<ManualDraftSeriesView> Series,
    IReadOnlyList<DraftWeekChange> Changes,
    IReadOnlyList<DraftSeriesExclusion> Exclusions, bool Replayed);

public static class ManualDraftViewMapper
{
    public static ManualDraftView Map(DemandRevisionDraft draft, bool replayed = false) =>
        new(draft.Id, draft.PlanningCycleId, draft.TenantId, draft.LegalEntityId,
            draft.PlanningPeriodKey, draft.AsOfDate, draft.CalendarId,
            draft.CalendarVersion, draft.TimeZoneId, draft.HorizonStart,
            draft.HorizonEnd, draft.State, draft.Version, draft.StateVersion,
            draft.CreatedBy, draft.CreatedAt, draft.CreationReason,
            draft.ReviewedBy, draft.ReviewedAt, draft.ReviewReason,
            draft.Series.Select(series => new ManualDraftSeriesView(
                series.SkuId, series.WarehouseId, series.BaseUomId,
                series.Weeks.Select(week => new ManualDraftWeekView(
                    week.Number, week.WeekStart, week.WeekEnd, week.ValueKind,
                    week.Quantity, week.Source, week.ManualReason,
                    week.ManualActorId, week.ManualAt)).ToArray(),
                series.Exclusion)).ToArray(),
            draft.Changes.ToArray(), draft.Exclusions.ToArray(), replayed);
}
