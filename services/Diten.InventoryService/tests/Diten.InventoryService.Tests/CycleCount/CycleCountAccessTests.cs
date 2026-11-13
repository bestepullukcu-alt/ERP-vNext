using Diten.InventoryService.Persistence.CycleCounting;
using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;
using static Diten.InventoryService.Tests.CycleCounting.CycleCountScenario;

namespace Diten.InventoryService.Tests.CycleCounting;

/// <summary>MOD-0181 DL-1 AC-5 — tenant and company isolation over the REAL host (scope middleware + repository predicates).</summary>
public sealed class CycleCountAccessTests(CycleCountApiFactory factory) : IClassFixture<CycleCountApiFactory>
{
    private static readonly string[] All = [Read, Create, Update, BulkDelete, Cancel, CountsRecord, VariancesRead];

    /// <summary>
    /// Every DL-1 endpoint of A's count, called by another TENANT that claims the SAME company id (so only the tenant predicate separates them)
    /// and by ANOTHER company of the same tenant (only the company predicate separates them): 404 each, nothing written, lists empty.
    /// </summary>
    [Fact]
    public async Task AC5_another_tenant_or_another_company_sees_404_on_every_endpoint_and_lists_nothing()
    {
        var s = new CycleCountScenario(factory);
        await s.ReceiveAsync(4, s.BinA);
        var draft = await s.CreatedAsync();
        var released = await s.ReleasedAsync(await s.CreatedAsync(s.Form(scope: [s.BinB, s.BinA])));
        var line = Assert.Single(await s.LinesAsync(Id(released)));
        var otherTenant = Guid.NewGuid();
        factory.TrackCountTenant(otherTenant);
        var otherCompany = Guid.NewGuid();
        factory.LegalEntities.RegisterName(otherCompany, "OTH", "Other company");

        foreach (var token in new[]
                 {
                     TestTokens.Mint(otherTenant, s.LegalEntity, s.User, All),
                     TestTokens.Mint(s.Tenant, otherCompany, s.User, All)
                 })
        {
            foreach (var id in new[] { Id(draft), Id(released) })
            {
                Assert.Equal("COUNT_NOT_FOUND", await ErrorCode(s.GetAsync($"/api/warehouse/cycle-counts/{id}", token), 404));
                Assert.Equal("COUNT_NOT_FOUND", await ErrorCode(s.GetAsync($"/api/warehouse/cycle-counts/{id}/lines", token), 404));
                Assert.Equal("COUNT_NOT_FOUND", await ErrorCode(s.GetAsync($"/api/warehouse/cycle-counts/{id}/events", token), 404));
                Assert.Equal("COUNT_NOT_FOUND", await ErrorCode(s.UpdateAsync(id, s.Form(), "W/\"1\"", token: token), 404));
                Assert.Equal("COUNT_NOT_FOUND", await ErrorCode(s.ReleaseAsync(id, "W/\"1\"", token: token), 404));
                Assert.Equal("COUNT_NOT_FOUND", await ErrorCode(s.CancelAsync(id, "W/\"2\"", token: token), 404));
                // DL-2: entry, found stock and submit
                Assert.Equal("COUNT_NOT_FOUND", await ErrorCode(s.RecordAsync(id, LineId(line), "1", "W/\"1\"", token: token), 404));
                Assert.Equal("COUNT_NOT_FOUND", await ErrorCode(s.AddLineAsync(id, s.FoundLine(s.BinB), token: token), 404));
                Assert.Equal("COUNT_NOT_FOUND", await ErrorCode(s.SubmitAsync(id, "W/\"2\"", token: token), 404));
            }

            var rows = await Expect(s.BulkDeleteAsync([(Id(draft), 1)], token: token), 200);
            Assert.Equal("NotFound", rows.GetProperty("results")[0].GetProperty("outcome").GetString());
            Assert.Equal(0, (await Expect(s.GetAsync("/api/warehouse/cycle-counts", token), 200)).GetProperty("total").GetInt64());
        }

        Assert.Equal(2, (await Expect(s.GetAsync("/api/warehouse/cycle-counts"), 200)).GetProperty("total").GetInt64());
        Assert.Equal("Draft", (await Expect(s.GetAsync($"/api/warehouse/cycle-counts/{Id(draft)}"), 200)).GetProperty("status").GetString());
        Assert.Single(s.Events(Id(draft)));
        Assert.Equal(2, s.Events(Id(released)).Count);
        Assert.Equal("Released", (await s.CurrentAsync(Id(released))).GetProperty("status").GetString());
        Assert.Single(s.StoredLines(Id(released)));
        Assert.True(s.StoredLines(Id(released))[0]["CountedQuantity"].IsBsonNull);
    }

    /// <summary>The keys are enforced: without the action's key the endpoint is 403 (the attribute, not the handler).</summary>
    [Fact]
    public async Task Each_endpoint_needs_its_own_key()
    {
        var s = new CycleCountScenario(factory);
        var count = await s.CreatedAsync();
        var readOnly = s.TokenFor(s.User, Read);
        Assert.Equal(403, (int)(await s.CreateAsync(token: readOnly)).StatusCode);
        Assert.Equal(403, (int)(await s.UpdateAsync(Id(count), s.Form(), ETag(count), token: readOnly)).StatusCode);
        Assert.Equal(403, (int)(await s.ReleaseAsync(Id(count), ETag(count), token: readOnly)).StatusCode);
        Assert.Equal(403, (int)(await s.CancelAsync(Id(count), ETag(count), token: readOnly)).StatusCode);
        Assert.Equal(403, (int)(await s.BulkDeleteAsync([(Id(count), 1)], token: readOnly)).StatusCode);
        Assert.Equal(403, (int)(await s.GetAsync("/api/warehouse/cycle-counts", s.TokenFor(s.User, Create))).StatusCode);
        Assert.Single(s.Stored(CycleCountCollections.Counts));

        // DL-2: entry, found stock and submit need cycle-count.counts.record — the DL-1 keys (and variances.read) are not enough
        await s.ReceiveAsync(2, s.BinA);
        var released = await s.ReleasedAsync(await s.CreatedAsync());
        var line = Assert.Single(await s.LinesAsync(Id(released)));
        var withoutRecord = s.TokenFor(s.User, Read, Create, Update, BulkDelete, Cancel, VariancesRead);
        Assert.Equal(403, (int)(await s.RecordAsync(Id(released), LineId(line), "2", ETag(line), token: withoutRecord)).StatusCode);
        Assert.Equal(403, (int)(await s.AddLineAsync(Id(released), s.FoundLine(s.BinA, s.AddSku()), token: withoutRecord)).StatusCode);
        Assert.Equal(403, (int)(await s.SubmitAsync(Id(released), ETag(released), token: withoutRecord)).StatusCode);
        Assert.Equal("Released", (await s.CurrentAsync(Id(released))).GetProperty("status").GetString());
        Assert.Single(s.StoredLines(Id(released)));
    }
}
