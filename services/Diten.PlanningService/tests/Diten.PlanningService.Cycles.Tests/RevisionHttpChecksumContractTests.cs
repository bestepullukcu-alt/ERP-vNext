using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.PlanningService.Application.Features.DemandPlanning;
using Diten.PlanningService.Domain.Features.DemandPlanning;
using Xunit;

namespace Diten.PlanningService.Cycles.Tests;

public sealed partial class PublishedRevisionMongoIntegrationTests
{
    [ManualDraftMongoFact]
    public async Task V2HttpManifestAndAllPages_IndependentlyRecomputeChecksum()
    {
        var (draft, _, _, _) = await PublishedFixture(secondSeries: false);
        await using var host = await ReadHttpHost.StartAsync(draft);
        var actor = Guid.NewGuid();
        var path = $"/api/v2/demand/revisions/{draft.Id:D}";
        using var manifestRequest = host.Request(path + "/manifest", draft.TenantId,
            draft.LegalEntityId, actor);
        using var manifestResponse = await host.Client.SendAsync(manifestRequest);
        var manifest = await SuccessData(manifestResponse);
        var rows = new List<JsonElement>();
        string? cursor = null;
        do
        {
            var suffix = cursor is null ? string.Empty : "&cursor=" + Uri.EscapeDataString(cursor);
            using var request = host.Request(path + "/rows?pageSize=17" + suffix,
                draft.TenantId, draft.LegalEntityId, actor);
            using var response = await host.Client.SendAsync(request);
            var page = await SuccessData(response);
            Assert.Equal(manifest.GetProperty("integrity").GetProperty("digest").GetString(),
                page.GetProperty("integrity").GetProperty("digest").GetString());
            Assert.Equal(manifest.GetProperty("stateVersion").GetInt32(),
                page.GetProperty("stateVersion").GetInt32());
            rows.AddRange(page.GetProperty("items").EnumerateArray().Select(item => item.Clone()));
            cursor = page.TryGetProperty("nextCursor", out var next) ? next.GetString() : null;
        } while (cursor is not null);

        Assert.Equal(manifest.GetProperty("expectedRowCount").GetInt32(), rows.Count);
        Assert.Equal(manifest.GetProperty("integrity").GetProperty("digest").GetString(),
            HttpContractDigest(manifest, rows));
        Assert.NotEqual(manifest.GetProperty("integrity").GetProperty("digest").GetString(),
            HttpContractDigest(manifest, rows, tamperFirstQuantity: true));
    }

    [ManualDraftMongoFact]
    public async Task V2HttpMethodRows_IndependentlyRecomputeCanonical2()
    {
        var (drafts, published, _) = Open();
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
        await using var host = await ReadHttpHost.StartAsync(draft);
        var actor = Guid.NewGuid();
        var path = $"/api/v2/demand/revisions/{draft.Id:D}";
        using var manifestRequest = host.Request(path + "/manifest", tenant, company, actor);
        using var manifestResponse = await host.Client.SendAsync(manifestRequest);
        var manifest = await SuccessData(manifestResponse);
        using var rowsRequest = host.Request(path + "/rows?pageSize=52", tenant, company, actor);
        using var rowsResponse = await host.Client.SendAsync(rowsRequest);
        var page = await SuccessData(rowsResponse);
        var rows = page.GetProperty("items").EnumerateArray().Select(x => x.Clone()).ToArray();
        Assert.Equal(52, rows.Length);
        Assert.Equal("MOD0188-CANONICAL-2/SHA-256",
            manifest.GetProperty("integrity").GetProperty("definitionId").GetString());
        Assert.Equal(manifest.GetProperty("integrity").GetProperty("digest").GetString(),
            HttpContractDigest(manifest, rows));
    }

    // Contract-only encoder: reads public JSON, not Mongo documents or producer helpers.
    private static string HttpContractDigest(JsonElement manifest,
        IReadOnlyCollection<JsonElement> rows, bool tamperFirstQuantity = false)
    {
        using var stream = new MemoryStream();
        void Put(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            Span<byte> length = stackalloc byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(length, bytes.Length);
            stream.Write(length);
            stream.Write(bytes);
        }
        void Value(JsonElement parent, string name) => Put(parent.GetProperty(name).GetString()!);
        void Id(JsonElement parent, string name) =>
            Put(parent.GetProperty(name).GetGuid().ToString("D").ToLowerInvariant());
        void Day(JsonElement parent, string name) =>
            Put(parent.GetProperty(name).GetDateTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        void Stamp(JsonElement parent, string name) => Put(parent.GetProperty(name)
            .GetDateTimeOffset().ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        void Number(JsonElement parent, string name) =>
            Put(parent.GetProperty(name).GetInt32().ToString(CultureInfo.InvariantCulture));

        var scheme = manifest.GetProperty("integrity").GetProperty("definitionId").GetString();
        var method = scheme == "MOD0188-CANONICAL-2/SHA-256";
        Assert.True(method || scheme == "MOD0188-CANONICAL-1/SHA-256");
        Put(method ? "MOD0188-CANONICAL-2" : "MOD0188-CANONICAL-1");
        Id(manifest, "tenantId"); Id(manifest, "legalEntityId"); Id(manifest, "revisionId");
        Id(manifest, "planningCycleId"); Value(manifest, "planningPeriodKey");
        var calendar = manifest.GetProperty("calendar");
        Day(calendar, "asOfDate"); Value(calendar, "calendarId");
        Value(calendar, "calendarVersion"); Value(calendar, "timeZoneId");
        Day(calendar, "horizonStart"); Day(calendar, "horizonEnd");
        Number(manifest, "contentVersion"); Number(manifest, "expectedPartCount");
        Number(manifest, "expectedRowCount");
        var trace = manifest.GetProperty("actorTrace");
        Id(trace, "preparedBy");
        var editors = trace.GetProperty("significantEditorIds").EnumerateArray()
            .Select(x => x.GetGuid()).OrderBy(x => x.ToString("D"), StringComparer.Ordinal)
            .ToArray();
        Put(editors.Length.ToString(CultureInfo.InvariantCulture));
        foreach (var editor in editors) Put(editor.ToString("D").ToLowerInvariant());
        Id(trace, "reviewedBy"); Stamp(trace, "reviewedAt"); Value(trace, "reviewReason");
        Id(trace, "publishedBy"); Stamp(trace, "publishedAt");
        foreach (var week in calendar.GetProperty("weeks").EnumerateArray()
                     .OrderBy(x => x.GetProperty("number").GetInt32()))
        {
            Number(week, "number"); Day(week, "weekStart"); Day(week, "weekEnd");
        }
        var excluded = manifest.GetProperty("excludedSeries").EnumerateArray()
            .OrderBy(x => x.GetProperty("skuId").GetString(), StringComparer.Ordinal)
            .ThenBy(x => x.GetProperty("warehouseId").GetString(), StringComparer.Ordinal)
            .ToArray();
        Put(excluded.Length.ToString(CultureInfo.InvariantCulture));
        foreach (var exclusion in excluded)
        {
            Id(exclusion, "skuId"); Value(exclusion, "warehouseId");
            Value(exclusion, "reason"); Id(exclusion, "excludedBy"); Stamp(exclusion, "excludedAt");
        }
        var ordered = rows.OrderBy(x => x.GetProperty("skuId").GetString(), StringComparer.Ordinal)
            .ThenBy(x => x.GetProperty("warehouseId").GetString(), StringComparer.Ordinal)
            .ThenBy(x => x.GetProperty("weekNumber").GetInt32()).ToArray();
        var first = true;
        foreach (var group in ordered.GroupBy(x => (x.GetProperty("skuId").GetString(),
                     x.GetProperty("warehouseId").GetString())))
        {
            var head = group.First();
            Id(head, "skuId"); Value(head, "warehouseId"); Value(head, "baseUomId");
            foreach (var row in group)
            {
                Number(row, "weekNumber"); Day(row, "weekStart"); Day(row, "weekEnd");
                Value(row, "valueKind");
                var quantity = decimal.Parse(row.GetProperty("grossQuantityBaseUom").GetString()!,
                    CultureInfo.InvariantCulture);
                if (first && tamperFirstQuantity) quantity += 1m;
                Put(quantity.ToString("G29", CultureInfo.InvariantCulture));
                first = false;
                Value(row, "origin");
                var manual = row.GetProperty("origin").GetString() == "Manual";
                Put(manual ? row.GetProperty("manualReason").GetString()! : string.Empty);
                Put(manual ? row.GetProperty("manualActorId").GetGuid().ToString("D").ToLowerInvariant()
                    : Guid.Empty.ToString("D"));
                Put(manual ? row.GetProperty("manualAt").GetDateTimeOffset().ToUniversalTime()
                    .ToString("O", CultureInfo.InvariantCulture) : string.Empty);
                if (!method) continue;
                var hasProvenance = row.TryGetProperty("methodProvenance", out var origin);
                Put(hasProvenance ? "1" : "0");
                if (!hasProvenance) continue;
                Id(origin, "forecastRunId"); Value(origin, "method"); Value(origin, "variant");
                Value(origin, "policyVersion"); Day(origin, "historyAsOfWeekStart");
                Value(origin, "historyContentSha256");
                Put(origin.GetProperty("unitVerificationPending").GetBoolean() ? "1" : "0");
                var parameters = origin.GetProperty("parameters").EnumerateObject()
                    .OrderBy(x => x.Name, StringComparer.Ordinal).ToArray();
                Put(parameters.Length.ToString(CultureInfo.InvariantCulture));
                foreach (var parameter in parameters)
                {
                    Put(parameter.Name); Put(parameter.Value.GetString()!);
                }
            }
        }
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }
}
