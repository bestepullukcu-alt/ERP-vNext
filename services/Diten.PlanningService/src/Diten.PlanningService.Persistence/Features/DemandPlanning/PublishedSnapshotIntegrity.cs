using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Diten.PlanningService.Domain.Features.DemandPlanning;

namespace Diten.PlanningService.Persistence.Features.DemandPlanning;

// MOD0188-CANONICAL-1: little-endian Int32 byte length followed by UTF-8 bytes for
// each ordered field below. Decimal uses invariant G29; dates use yyyy-MM-dd; GUIDs
// use lower-case D. Lifecycle state, physical IDs and timestamps are excluded.
// The same routine verifies persisted parts, so a partial snapshot is never returned.
public static class PublishedSnapshotIntegrity
{
    public static (PublishedRevisionManifest Manifest, PublishedRevisionPart[] Parts) Build(
        DemandRevisionDraft draft, Guid publisherId, DateTimeOffset publishedAt)
    {
        if (draft.State != DemandRevisionState.Approved ||
            !draft.HasCompleteKnownQuantities || publisherId == Guid.Empty ||
            publishedAt == default || draft.HasSignificantContribution(publisherId))
            throw new InvalidDataException("Only a complete independently prepared Approved revision can publish.");
        var selected = draft.Series.Where(x => x.Exclusion is null)
            .OrderBy(x => x.SkuId.ToString("D"), StringComparer.Ordinal)
            .ThenBy(x => x.WarehouseId, StringComparer.Ordinal).ToArray();
        var parts = selected.Select(series => new PublishedRevisionPart
        {
            TenantId = draft.TenantId, LegalEntityId = draft.LegalEntityId,
            RevisionId = draft.Id, SkuId = series.SkuId,
            WarehouseId = series.WarehouseId, BaseUomId = series.BaseUomId,
            Rows = series.Weeks.OrderBy(x => x.Number).Select(week =>
                new PublishedWeekRow(week.Number, week.WeekStart, week.WeekEnd,
                    week.ValueKind, week.Quantity, week.Source,
                    week.ManualReason, week.ManualActorId, week.ManualAt,
                    week.MethodProvenance?.Copy())).ToList(),
            CreatedAt = publishedAt
        }).ToArray();
        var manifest = new PublishedRevisionManifest
        {
            TenantId = draft.TenantId, LegalEntityId = draft.LegalEntityId,
            RevisionId = draft.Id, PlanningCycleId = draft.PlanningCycleId,
            PlanningPeriodKey = draft.PlanningPeriodKey,
            AsOfDate = draft.AsOfDate, CalendarId = draft.CalendarId,
            CalendarVersion = draft.CalendarVersion, TimeZoneId = draft.TimeZoneId,
            HorizonStart = draft.HorizonStart, HorizonEnd = draft.HorizonEnd,
            Weeks = draft.Weeks.ToList(),
            SelectedSeries = selected.Select(x => new PublishedSeriesScope(
                x.SkuId, x.WarehouseId, x.BaseUomId)).ToList(),
            ExcludedSeries = draft.Exclusions.ToList(),
            ContentVersion = draft.Version, StateVersion = draft.StateVersion + 1,
            ExpectedPartCount = parts.Length, ExpectedRowCount = parts.Length * 52,
            CreatedBy = draft.CreatedBy, PreparedAt = draft.CreatedAt,
            SignificantEditorIds = draft.Changes.Select(x => x.ActorId)
                .Concat(draft.NewExclusions.Select(x => x.ActorId))
                .Concat(draft.SourceContributorIds)
                .Distinct().OrderBy(x => x.ToString("D"), StringComparer.Ordinal).ToList(),
            ReviewedBy = draft.ReviewedBy, ReviewedAt = draft.ReviewedAt,
            ReviewReason = draft.ReviewReason,
            PublishedBy = publisherId, PublishedAt = publishedAt, CreatedAt = publishedAt
        };
        if (parts.Any(part => part.Rows.Any(row => row.Source == DraftWeekSource.Method)))
            manifest.ChecksumScheme = "MOD0188-CANONICAL-2/SHA-256";
        manifest.Checksum = Calculate(manifest, parts);
        return (manifest, parts);
    }

    public static string Calculate(PublishedRevisionManifest manifest,
        IReadOnlyCollection<PublishedRevisionPart> parts)
    {
        var methodScheme = manifest.ChecksumScheme == "MOD0188-CANONICAL-2/SHA-256";
        if (!methodScheme && manifest.ChecksumScheme != "MOD0188-CANONICAL-1/SHA-256")
            throw new InvalidDataException("Published revision checksum scheme is unsupported.");
        if (manifest.TenantId == Guid.Empty || manifest.LegalEntityId == Guid.Empty ||
            manifest.RevisionId == Guid.Empty || manifest.PlanningCycleId == Guid.Empty ||
            manifest.Weeks.Count != 52 || manifest.ExpectedPartCount < 1 ||
            parts.Count != manifest.ExpectedPartCount ||
            manifest.ExpectedRowCount != parts.Count * 52 ||
            manifest.PlanningPeriodKey != manifest.Weeks[0].WeekStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ||
            manifest.HorizonStart != manifest.Weeks[0].WeekStart ||
            manifest.HorizonEnd != manifest.Weeks[^1].WeekEnd ||
            manifest.SelectedSeries.Count != parts.Count ||
            manifest.SelectedSeries.Select(x => (x.SkuId, x.WarehouseId)).Distinct().Count() != parts.Count ||
            manifest.CreatedBy == Guid.Empty ||
            manifest.ReviewedBy is null ||
            manifest.PublishedBy == Guid.Empty || manifest.PublishedAt == default ||
            manifest.ReviewedAt is null || string.IsNullOrWhiteSpace(manifest.ReviewReason) ||
            manifest.SignificantEditorIds.Distinct().Count() != manifest.SignificantEditorIds.Count)
            throw new InvalidDataException("Published revision manifest is incomplete or inconsistent.");
        var ordered = parts.OrderBy(x => x.SkuId.ToString("D"), StringComparer.Ordinal)
            .ThenBy(x => x.WarehouseId, StringComparer.Ordinal).ToArray();
        if (ordered.Select(x => (x.SkuId, x.WarehouseId)).Distinct().Count() != parts.Count)
            throw new InvalidDataException("Published revision parts contain duplicate series.");
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        void Field(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }
        void GuidField(Guid value) => Field(value.ToString("D"));
        void DateField(DateOnly value) => Field(value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        Field(methodScheme ? "MOD0188-CANONICAL-2" : "MOD0188-CANONICAL-1");
        GuidField(manifest.TenantId); GuidField(manifest.LegalEntityId);
        GuidField(manifest.RevisionId); GuidField(manifest.PlanningCycleId);
        Field(manifest.PlanningPeriodKey); DateField(manifest.AsOfDate);
        Field(manifest.CalendarId); Field(manifest.CalendarVersion); Field(manifest.TimeZoneId);
        DateField(manifest.HorizonStart); DateField(manifest.HorizonEnd);
        Field(manifest.ContentVersion.ToString(CultureInfo.InvariantCulture));
        Field(manifest.ExpectedPartCount.ToString(CultureInfo.InvariantCulture));
        Field(manifest.ExpectedRowCount.ToString(CultureInfo.InvariantCulture));
        GuidField(manifest.CreatedBy);
        Field(manifest.SignificantEditorIds.Count.ToString(CultureInfo.InvariantCulture));
        foreach (var editor in manifest.SignificantEditorIds.OrderBy(x => x.ToString("D"), StringComparer.Ordinal))
            GuidField(editor);
        GuidField(manifest.ReviewedBy.Value);
        Field(manifest.ReviewedAt.Value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        Field(manifest.ReviewReason);
        GuidField(manifest.PublishedBy);
        Field(manifest.PublishedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        for (var index = 0; index < 52; index++)
        {
            var week = manifest.Weeks[index];
            if (week.Number != index + 1 || week.WeekStart > week.WeekEnd ||
                (index > 0 && week.WeekStart != manifest.Weeks[index - 1].WeekEnd.AddDays(1)))
                throw new InvalidDataException("Published week boundaries are incomplete.");
            Field(week.Number.ToString(CultureInfo.InvariantCulture));
            DateField(week.WeekStart); DateField(week.WeekEnd);
        }
        var exclusions = manifest.ExcludedSeries.OrderBy(x => x.SkuId.ToString("D"), StringComparer.Ordinal)
            .ThenBy(x => x.WarehouseId, StringComparer.Ordinal).ToArray();
        Field(exclusions.Length.ToString(CultureInfo.InvariantCulture));
        foreach (var exclusion in exclusions)
        {
            GuidField(exclusion.SkuId); Field(exclusion.WarehouseId);
            Field(exclusion.Reason); GuidField(exclusion.ActorId);
            Field(exclusion.OccurredAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        }
        for (var index = 0; index < ordered.Length; index++)
        {
            var part = ordered[index];
            var scope = manifest.SelectedSeries.OrderBy(x => x.SkuId.ToString("D"), StringComparer.Ordinal)
                .ThenBy(x => x.WarehouseId, StringComparer.Ordinal).ElementAt(index);
            if (part.TenantId != manifest.TenantId || part.LegalEntityId != manifest.LegalEntityId ||
                part.RevisionId != manifest.RevisionId || part.SkuId == Guid.Empty ||
                part.SkuId != scope.SkuId || part.WarehouseId != scope.WarehouseId ||
                part.BaseUomId != scope.BaseUomId || string.IsNullOrWhiteSpace(part.BaseUomId) ||
                part.Rows.Count != 52)
                throw new InvalidDataException("Published revision part scope or count is invalid.");
            GuidField(part.SkuId); Field(part.WarehouseId); Field(part.BaseUomId);
            for (var weekIndex = 0; weekIndex < 52; weekIndex++)
            {
                var row = part.Rows[weekIndex];
                var week = manifest.Weeks[weekIndex];
                if (row.Number != weekIndex + 1 || row.WeekStart != week.WeekStart ||
                    row.WeekEnd != week.WeekEnd || row.ValueKind != DraftWeekValueKind.Known ||
                    row.Quantity is null or < 0 ||
                    (row.Source == DraftWeekSource.Manual &&
                        (row.ManualActorId == Guid.Empty || row.ManualAt == default ||
                         string.IsNullOrWhiteSpace(row.ManualReason) ||
                         row.MethodProvenance is not null)) ||
                    (row.Source is not (DraftWeekSource.Manual or DraftWeekSource.Method)) ||
                    (row.Source == DraftWeekSource.Method &&
                        (!methodScheme || row.MethodProvenance is not { IsValid: true } ||
                         row.ManualActorId != Guid.Empty || row.ManualAt != default ||
                         !string.IsNullOrEmpty(row.ManualReason))))
                    throw new InvalidDataException("Published revision row is missing or invalid.");
                Field(row.Number.ToString(CultureInfo.InvariantCulture));
                DateField(row.WeekStart); DateField(row.WeekEnd);
                Field(row.ValueKind.ToString());
                Field(row.Quantity.Value.ToString("G29", CultureInfo.InvariantCulture));
                Field(row.Source.ToString());
                Field(row.ManualReason); GuidField(row.ManualActorId);
                Field(row.Source == DraftWeekSource.Method ? string.Empty :
                    row.ManualAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
                if (methodScheme)
                {
                    var provenance = row.MethodProvenance;
                    Field(provenance is null ? "0" : "1");
                    if (provenance is not null)
                    {
                        GuidField(provenance.ForecastRunId);
                        Field(provenance.Method); Field(provenance.Variant);
                        Field(provenance.PolicyVersion);
                        DateField(provenance.HistoryAsOfWeekStart);
                        Field(provenance.HistoryContentSha256);
                        Field(provenance.UnitVerificationPending ? "1" : "0");
                        Field(provenance.Parameters.Count.ToString(CultureInfo.InvariantCulture));
                        foreach (var parameter in provenance.Parameters.OrderBy(
                                     item => item.Key, StringComparer.Ordinal))
                        {
                            Field(parameter.Key); Field(parameter.Value);
                        }
                    }
                }
            }
        }
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }
}
