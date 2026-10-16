using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Diten.PlanningService.Application.Features.DemandPlanning;
using Diten.PlanningService.Domain.Features.DemandPlanning;
using Diten.PlanningService.Persistence.Features.DemandPlanning;
using MongoDB.Driver;
using Xunit;

namespace Diten.PlanningService.Cycles.Tests;

// Contract-side encoder: follows demand-v2.openapi.yaml x-checksum-canonicalization.
// Deliberately does not invoke PublishedSnapshotIntegrity.Calculate or its helpers.
public sealed partial class PublishedRevisionMongoIntegrationTests
{
    [ManualDraftMongoFact]
    public async Task ContractChecksum_Canonical1_RecomputesFromPersistedMongoDocuments()
    {
        var (drafts, published, context) = Open();
        var tenant = Guid.NewGuid(); var company = Guid.NewGuid();
        var creator = Guid.NewGuid(); var reviewer = Guid.NewGuid();
        var draft = Draft(tenant, company, creator, FirstWeek);
        await Approve(drafts, draft, creator, reviewer);
        Assert.Equal(PublishOutcome.Published, (await Publish(published, draft, reviewer)).Outcome);
        var (manifest, parts) = await ReadPersisted(tenant, company, draft.Id, context);
        Assert.Equal("MOD0188-CANONICAL-1/SHA-256", manifest.ChecksumScheme);
        Assert.Equal(manifest.Checksum, ContractDigest(manifest, parts));
        parts[0].Rows[0] = parts[0].Rows[0] with { Quantity = parts[0].Rows[0].Quantity + 1m };
        Assert.NotEqual(manifest.Checksum, ContractDigest(manifest, parts));
    }

    [ManualDraftMongoFact]
    public async Task ContractChecksum_Canonical2_RecomputesAndDetectsMethodProvenanceChange()
    {
        var (drafts, published, context) = Open();
        var tenant = Guid.NewGuid(); var company = Guid.NewGuid();
        var creator = Guid.NewGuid(); var reviewer = Guid.NewGuid();
        var template = Draft(tenant, company, creator, FirstWeek);
        var selected = Assert.Single(template.Series);
        var provenance = new MethodWeekProvenance(Guid.NewGuid(), "Naive", "last-value",
            "policy-1", FirstWeek.AddDays(-7), new string('A', 64),
            new Dictionary<string, string> { ["horizon"] = "52" }, true);
        var cycle = new PlanningCycle
        {
            Id = template.PlanningCycleId, TenantId = tenant, LegalEntityId = company,
            AsOfDate = template.AsOfDate, CalendarId = template.CalendarId,
            CalendarVersion = template.CalendarVersion, TimeZoneId = template.TimeZoneId,
            PlanningPeriodKey = template.PlanningPeriodKey,
            HorizonStart = template.HorizonStart, HorizonEnd = template.HorizonEnd,
            Weeks = template.Weeks.ToList()
        };
        var draft = Assert.IsType<DemandRevisionDraft>(ManualDraftFactory.Create(cycle,
            tenant, company, creator, "Forecast reviewed", Now,
            [new VerifiedDraftSeries(selected.SkuId, selected.WarehouseId,
                selected.BaseUomId, selected.Weeks.Select(week =>
                    new ManualDraftWeekInput(week.Number, week.WeekStart, week.WeekEnd,
                        week.ValueKind, week.Quantity)).ToArray(), provenance)]).Data);
        await Approve(drafts, draft, creator, reviewer);
        Assert.Equal(PublishOutcome.Published, (await Publish(published, draft, reviewer)).Outcome);
        var (manifest, parts) = await ReadPersisted(tenant, company, draft.Id, context);
        Assert.Equal("MOD0188-CANONICAL-2/SHA-256", manifest.ChecksumScheme);
        Assert.Equal(manifest.Checksum, ContractDigest(manifest, parts));
        parts[0].Rows[0] = parts[0].Rows[0] with
        {
            MethodProvenance = provenance with { PolicyVersion = "changed-policy" }
        };
        Assert.NotEqual(manifest.Checksum, ContractDigest(manifest, parts));
    }

    private static async Task<(PublishedRevisionManifest Manifest, PublishedRevisionPart[] Parts)>
        ReadPersisted(Guid tenant, Guid company, Guid revision, DemandPlanningMongoContext context)
    {
        var manifest = await context.PublishedRevisionManifests.Find(x =>
            x.TenantId == tenant && x.LegalEntityId == company && x.RevisionId == revision)
            .SingleAsync();
        var parts = await context.PublishedRevisionParts.Find(x =>
            x.TenantId == tenant && x.LegalEntityId == company && x.RevisionId == revision)
            .ToListAsync();
        return (manifest, parts.ToArray());
    }

    private static string ContractDigest(PublishedRevisionManifest manifest,
        IReadOnlyCollection<PublishedRevisionPart> parts)
    {
        using var bytes = new MemoryStream();
        void Put(string value)
        {
            var text = Encoding.UTF8.GetBytes(value);
            Span<byte> length = stackalloc byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(length, text.Length);
            bytes.Write(length);
            bytes.Write(text);
        }
        void Id(Guid value) => Put(value.ToString("D").ToLowerInvariant());
        void Day(DateOnly value) => Put(value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        void Stamp(DateTimeOffset value) =>
            Put(value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        void Number(int value) => Put(value.ToString(CultureInfo.InvariantCulture));
        var method = manifest.ChecksumScheme == "MOD0188-CANONICAL-2/SHA-256";
        Put(method ? "MOD0188-CANONICAL-2" : "MOD0188-CANONICAL-1");
        Id(manifest.TenantId); Id(manifest.LegalEntityId); Id(manifest.RevisionId);
        Id(manifest.PlanningCycleId); Put(manifest.PlanningPeriodKey); Day(manifest.AsOfDate);
        Put(manifest.CalendarId); Put(manifest.CalendarVersion); Put(manifest.TimeZoneId);
        Day(manifest.HorizonStart); Day(manifest.HorizonEnd); Number(manifest.ContentVersion);
        Number(manifest.ExpectedPartCount); Number(manifest.ExpectedRowCount); Id(manifest.CreatedBy);
        Number(manifest.SignificantEditorIds.Count);
        foreach (var editor in manifest.SignificantEditorIds.OrderBy(x => x.ToString("D"), StringComparer.Ordinal))
            Id(editor);
        Id(manifest.ReviewedBy!.Value); Stamp(manifest.ReviewedAt!.Value);
        Put(manifest.ReviewReason!); Id(manifest.PublishedBy); Stamp(manifest.PublishedAt);
        foreach (var week in manifest.Weeks.OrderBy(x => x.Number))
        {
            Number(week.Number); Day(week.WeekStart); Day(week.WeekEnd);
        }
        var exclusions = manifest.ExcludedSeries.OrderBy(x => x.SkuId.ToString("D"), StringComparer.Ordinal)
            .ThenBy(x => x.WarehouseId, StringComparer.Ordinal).ToArray();
        Number(exclusions.Length);
        foreach (var exclusion in exclusions)
        {
            Id(exclusion.SkuId); Put(exclusion.WarehouseId); Put(exclusion.Reason);
            Id(exclusion.ActorId); Stamp(exclusion.OccurredAt);
        }
        foreach (var part in parts.OrderBy(x => x.SkuId.ToString("D"), StringComparer.Ordinal)
                     .ThenBy(x => x.WarehouseId, StringComparer.Ordinal))
        {
            Id(part.SkuId); Put(part.WarehouseId); Put(part.BaseUomId);
            foreach (var row in part.Rows.OrderBy(x => x.Number))
            {
                Number(row.Number); Day(row.WeekStart); Day(row.WeekEnd);
                Put(row.ValueKind.ToString());
                Put(row.Quantity!.Value.ToString("G29", CultureInfo.InvariantCulture));
                Put(row.Source.ToString()); Put(row.ManualReason); Id(row.ManualActorId);
                Put(row.Source == DraftWeekSource.Method ? string.Empty :
                    row.ManualAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
                if (!method) continue;
                var origin = row.MethodProvenance;
                Put(origin is null ? "0" : "1");
                if (origin is null) continue;
                Id(origin.ForecastRunId); Put(origin.Method); Put(origin.Variant);
                Put(origin.PolicyVersion); Day(origin.HistoryAsOfWeekStart);
                Put(origin.HistoryContentSha256); Put(origin.UnitVerificationPending ? "1" : "0");
                Number(origin.Parameters.Count);
                foreach (var parameter in origin.Parameters.OrderBy(x => x.Key, StringComparer.Ordinal))
                {
                    Put(parameter.Key); Put(parameter.Value);
                }
            }
        }
        return Convert.ToHexString(SHA256.HashData(bytes.ToArray()));
    }
}