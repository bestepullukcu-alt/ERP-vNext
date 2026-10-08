using System.Text.Json;
using Diten.PlanningService.Domain.Features.DemandPlanning;
using MongoDB.Driver;

namespace Diten.PlanningService.Persistence.Features.DemandPlanning;

public sealed partial class ManualDraftMongoStore
{
    internal async Task<DemandRevisionDraft> CreateFromPublishedAsync(
        IClientSessionHandle session, PublishedRevisionManifest source,
        IReadOnlyList<PublishedRevisionPart> sourceParts, Guid actorId,
        string reason, string requestKey, DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        var selected = sourceParts.Select(part => new RollbackSeriesSnapshot(
            part.SkuId, part.WarehouseId, part.BaseUomId,
            part.Rows.Select(row => new RollbackWeekSnapshot(row.Number,
                row.WeekStart, row.WeekEnd, row.ValueKind, row.Quantity,
                row.Source, row.ManualReason, row.ManualActorId, row.ManualAt)).ToArray(),
            null)).ToArray();
        var excluded = source.ExcludedSeries.Select(item => new RollbackSeriesSnapshot(
            item.SkuId, item.WarehouseId, string.Empty,
            Array.Empty<RollbackWeekSnapshot>(), item)).ToArray();
        var series = selected.Concat(excluded).OrderBy(x => x.SkuId)
            .ThenBy(x => x.WarehouseId, StringComparer.Ordinal).ToArray();
        if (series.Select(x => (x.SkuId, x.WarehouseId)).Distinct().Count() != series.Length ||
            selected.Length == 0 || series.Any(x => x.SourceExclusion is null &&
                (x.Weeks.Count != 52 || x.Weeks.Any(w => w.Source != DraftWeekSource.Manual ||
                    w.ValueKind != DraftWeekValueKind.Known || w.Quantity is null or < 0 ||
                    w.ManualActorId == Guid.Empty || w.ManualAt == default ||
                    string.IsNullOrWhiteSpace(w.ManualReason)))))
            throw new InvalidDataException("Source series cannot be copied without provenance.");

        var cycle = new PlanningCycle
        {
            Id = source.PlanningCycleId, TenantId = source.TenantId,
            LegalEntityId = source.LegalEntityId, AsOfDate = source.AsOfDate,
            CalendarId = source.CalendarId, CalendarVersion = source.CalendarVersion,
            TimeZoneId = source.TimeZoneId, HorizonStart = source.HorizonStart,
            HorizonEnd = source.HorizonEnd, PlanningPeriodKey = source.PlanningPeriodKey,
            Weeks = source.Weeks.Select(w => new PlanningWeek
            {
                Number = w.Number, WeekStart = w.WeekStart, WeekEnd = w.WeekEnd
            }).ToList()
        };
        var cycleJson = JsonSerializer.Serialize(cycle, JsonOptions);
        var seriesJson = series.Select(x => JsonSerializer.Serialize(x, JsonOptions)).ToArray();
        var copiedScopeJson = JsonSerializer.Serialize(new RollbackScopeSnapshot(
            source.SelectedSeries, source.ExcludedSeries), JsonOptions);
        var normalizedReason = reason.Trim();
        var normalizedKey = requestKey.Trim();
        var fingerprint = RollbackFingerprint(cycleJson, seriesJson,
            source.RevisionId, source.State, source.StateVersion, source.Checksum,
            copiedScopeJson, actorId, normalizedReason);
        var newId = Guid.NewGuid();
        var manifest = new ManualDraftManifest
        {
            Id = newId, TenantId = source.TenantId,
            LegalEntityId = source.LegalEntityId, PlanningCycleId = cycle.Id,
            CycleJson = cycleJson, CreateRequestKey = normalizedKey,
            CreateFingerprint = fingerprint, CreationReason = normalizedReason,
            CreatedBy = actorId, CreatedAt = occurredAt,
            ExpectedPartCount = series.Length, SourceRevisionId = source.RevisionId,
            SourceState = source.State, SourceStateVersion = source.StateVersion,
            SourceChecksum = source.Checksum, CopiedScopeJson = copiedScopeJson
        };
        var parts = series.Zip(seriesJson, (item, json) => new ManualDraftSeriesPart
        {
            TenantId = source.TenantId, LegalEntityId = source.LegalEntityId,
            RevisionId = newId, SkuId = item.SkuId,
            WarehouseId = item.WarehouseId, SourceSeriesJson = json,
            CreatedAt = occurredAt
        }).ToArray();
        var audit = new ManualDraftAuditRecord
        {
            TenantId = source.TenantId, LegalEntityId = source.LegalEntityId,
            RevisionId = newId, RequestKey = normalizedKey,
            Fingerprint = fingerprint, Action = "Created", VersionAfter = 0,
            ActorId = actorId, OccurredAt = occurredAt, Reason = normalizedReason,
            CreatedAt = occurredAt, SourceRevisionId = source.RevisionId,
            SourceState = source.State, SourceStateVersion = source.StateVersion,
            SourceChecksum = source.Checksum, CopiedScopeJson = copiedScopeJson
        };
        EnsureDocumentFits(manifest);
        EnsureDocumentFits(audit);
        foreach (var part in parts) EnsureDocumentFits(part);
        await context.ManualDraftManifests.InsertOneAsync(session, manifest,
            cancellationToken: cancellationToken);
        await context.ManualDraftSeriesParts.InsertManyAsync(session, parts,
            cancellationToken: cancellationToken);
        await context.ManualDraftAudit.InsertOneAsync(session, audit,
            cancellationToken: cancellationToken);
        return RestoreRollbackDraft(manifest, cycle, parts, source.TenantId,
            source.LegalEntityId, newId);
    }

    private static DemandRevisionDraft RestoreRollbackDraft(ManualDraftManifest manifest,
        PlanningCycle cycle, IReadOnlyList<ManualDraftSeriesPart> parts,
        Guid tenantId, Guid legalEntityId, Guid revisionId)
    {
        if (manifest.SourceRevisionId is not { } sourceId || sourceId == Guid.Empty ||
            manifest.SourceState is not (DemandRevisionState.Published or
                DemandRevisionState.Superseded) ||
            manifest.SourceStateVersion is null ||
            string.IsNullOrWhiteSpace(manifest.SourceChecksum) ||
            string.IsNullOrWhiteSpace(manifest.CopiedScopeJson) ||
            cycle.Weeks.Count != 52)
            throw new InvalidDataException("Rollback source lineage is incomplete.");
        var items = parts.OrderBy(x => x.SkuId).ThenBy(x => x.WarehouseId,
            StringComparer.Ordinal).Select(part =>
        {
            if (part.TenantId != tenantId || part.LegalEntityId != legalEntityId ||
                part.RevisionId != revisionId || part.SkuId == Guid.Empty ||
                string.IsNullOrWhiteSpace(part.WarehouseId) ||
                string.IsNullOrWhiteSpace(part.SourceSeriesJson) ||
                !string.IsNullOrEmpty(part.InitialSeriesJson))
                throw new InvalidDataException("Rollback physical part scope is invalid.");
            var item = JsonSerializer.Deserialize<RollbackSeriesSnapshot>(
                part.SourceSeriesJson, JsonOptions) ??
                throw new InvalidDataException("Rollback series cannot be decoded.");
            if (item.SkuId != part.SkuId || item.WarehouseId != part.WarehouseId ||
                item.Weeks is null)
                throw new InvalidDataException("Rollback part identity differs from content.");
            return item;
        }).ToArray();
        if (items.Select(x => (x.SkuId, x.WarehouseId)).Distinct().Count() != items.Length)
            throw new InvalidDataException("Rollback series are duplicated.");
        var scope = JsonSerializer.Deserialize<RollbackScopeSnapshot>(
            manifest.CopiedScopeJson, JsonOptions) ??
            throw new InvalidDataException("Rollback scope cannot be decoded.");
        var selected = items.Where(x => x.SourceExclusion is null).ToArray();
        var excluded = items.Where(x => x.SourceExclusion is not null).ToArray();
        if (selected.Length == 0 ||
            !selected.Select(x => (x.SkuId, x.WarehouseId, x.BaseUomId)).OrderBy(x => x.SkuId)
                .ThenBy(x => x.WarehouseId, StringComparer.Ordinal)
                .SequenceEqual(scope.SelectedSeries.Select(x => (x.SkuId, x.WarehouseId,
                    x.BaseUomId)).OrderBy(x => x.SkuId).ThenBy(x => x.WarehouseId,
                    StringComparer.Ordinal)) ||
            !excluded.Select(x => x.SourceExclusion).OrderBy(x => x!.SkuId)
                .ThenBy(x => x!.WarehouseId, StringComparer.Ordinal)
                .SequenceEqual(scope.ExcludedSeries.OrderBy(x => x.SkuId)
                    .ThenBy(x => x.WarehouseId, StringComparer.Ordinal)) ||
            excluded.Any(x => x.Weeks.Count != 0 ||
                x.SourceExclusion!.SkuId != x.SkuId ||
                x.SourceExclusion.WarehouseId != x.WarehouseId))
            throw new InvalidDataException("Rollback scope differs from source lineage.");
        var computed = RollbackFingerprint(manifest.CycleJson,
            parts.OrderBy(x => x.SkuId).ThenBy(x => x.WarehouseId,
                StringComparer.Ordinal).Select(x => x.SourceSeriesJson!).ToArray(),
            sourceId, manifest.SourceState.Value, manifest.SourceStateVersion.Value,
            manifest.SourceChecksum, manifest.CopiedScopeJson,
            manifest.CreatedBy, manifest.CreationReason);
        if (computed != manifest.CreateFingerprint)
            throw new InvalidDataException("Rollback content fingerprint differs from creation audit.");
        var restored = items.Select(item =>
        {
            if (item.SourceExclusion is null &&
                (string.IsNullOrWhiteSpace(item.BaseUomId) || item.Weeks.Count != 52))
                throw new InvalidDataException("Rollback selected series is incomplete.");
            var weeks = item.Weeks.Select((week, index) =>
            {
                var boundary = cycle.Weeks[index];
                if (week.Number != boundary.Number ||
                    week.WeekStart != boundary.WeekStart || week.WeekEnd != boundary.WeekEnd ||
                    week.Source != DraftWeekSource.Manual ||
                    week.ValueKind != DraftWeekValueKind.Known || week.Quantity is null or < 0 ||
                    week.ManualActorId == Guid.Empty || week.ManualAt == default ||
                    string.IsNullOrWhiteSpace(week.ManualReason))
                    throw new InvalidDataException("Rollback week cannot preserve source provenance.");
                return new DemandDraftWeek(week.Number, week.WeekStart, week.WeekEnd,
                    week.ValueKind, week.Quantity, week.ManualReason,
                    week.ManualActorId, week.ManualAt);
            }).ToArray();
            return new DemandDraftSeries
            {
                SkuId = item.SkuId, WarehouseId = item.WarehouseId,
                BaseUomId = item.BaseUomId, Weeks = weeks
            };
        }).ToArray();
        var draft = new DemandRevisionDraft
        {
            Id = revisionId, TenantId = tenantId, LegalEntityId = legalEntityId,
            PlanningCycleId = cycle.Id, PlanningPeriodKey = cycle.PlanningPeriodKey,
            AsOfDate = cycle.AsOfDate, CalendarId = cycle.CalendarId,
            CalendarVersion = cycle.CalendarVersion, TimeZoneId = cycle.TimeZoneId,
            HorizonStart = cycle.HorizonStart, HorizonEnd = cycle.HorizonEnd,
            Weeks = cycle.Weeks, Series = restored, CreatedBy = manifest.CreatedBy,
            CreatedAt = manifest.CreatedAt, CreationReason = manifest.CreationReason
        };
        draft.RestoreSourceExclusions(scope.ExcludedSeries);
        return draft;
    }

    internal static string RollbackFingerprint(string cycleJson,
        IReadOnlyList<string> seriesJson, Guid sourceRevisionId,
        DemandRevisionState sourceState, int sourceStateVersion,
        string sourceChecksum, string scopeJson, Guid actorId, string reason) =>
        Hash(cycleJson + "\n" + string.Join("\n", seriesJson) + "\n" +
            sourceRevisionId.ToString("D") + "\n" + sourceState + "\n" +
            sourceStateVersion + "\n" + sourceChecksum + "\n" + scopeJson +
            "\n" + actorId.ToString("D") + "\n" + reason.Trim());
}
