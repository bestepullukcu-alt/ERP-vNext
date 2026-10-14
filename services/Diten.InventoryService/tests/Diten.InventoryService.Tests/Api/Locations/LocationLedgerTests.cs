using Diten.InventoryService.Persistence;
using Diten.InventoryService.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Diten.InventoryService.Tests.Api.Locations;

/// <summary>
/// MOD-0173-FU02 ↔ ledger: K-13 (AC-13 — Inactive / Archived stay readable with their status, the ledger refuses
/// postings to them, a REVERSAL included, via the in-transaction check) and AC-16 (stale If-Match → 409; a posting
/// racing a deactivation never leaves stock on an Inactive location; the transaction re-runs are counted).
/// </summary>
public sealed class LocationLedgerTests : IClassFixture<InventoryApiFactory>
{
    private readonly InventoryApiFactory _factory;
    private readonly ITestOutputHelper _output;

    public LocationLedgerTests(InventoryApiFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    [Fact]
    public async Task AC13_inactive_and_archived_locations_stay_readable_with_their_status()
    {
        var s = new InventoryScenario(_factory);
        var api = new LocationApi(s);
        await api.Expect(api.StatusAsync(s.LocationA, "Inactive", "W/\"1\""), 200);
        await api.Expect(api.StatusAsync(s.LocationB, "Archived", "W/\"1\""), 200);

        Assert.Equal("Inactive", (await api.Get(s.LocationA)).GetProperty("status").GetString());
        Assert.Equal("Archived", (await api.Get(s.LocationB)).GetProperty("status").GetString());
        var children = await api.Expect(api.GetAsync($"/api/locations?parentId={s.StorageNode.Id:D}"), 200);
        Assert.Equal(["Archived", "Inactive"], children.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("status").GetString()).OrderBy(x => x).ToList());
        var validation = await api.Expect(api.ValidateAsync(s.LocationA, s.LocationB), 200);
        Assert.All(validation.GetProperty("results").EnumerateArray(), r => Assert.True(r.GetProperty("exists").GetBoolean()));
    }

    [Fact]
    public async Task AC13_the_ledger_refuses_postings_to_inactive_or_archived_locations_with_422_UNKNOWN_LOCATION()
    {
        var s = new InventoryScenario(_factory);
        var api = new LocationApi(s);
        await api.Expect(api.StatusAsync(s.LocationA, "Inactive", "W/\"1\""), 200);
        await api.Expect(api.StatusAsync(s.LocationB, "Archived", "W/\"1\""), 200);

        foreach (var location in new[] { s.LocationA, s.LocationB })
        {
            var refused = await s.PostExpectingAsync(s.Receipt("1", location: location), 422);
            Assert.Equal("UNKNOWN_LOCATION", refused.GetProperty("error").GetProperty("code").GetString());
        }

        Assert.Empty(s.Persisted("inventory_transactions"));
        await s.PostExpectingAsync(s.Receipt("1", location: s.LocationC), 201); // control
    }

    /// <summary>
    /// A REVERSAL copies its original and is NOT validated against the location master beforehand (MOD-0173
    /// ASSUMPTION-0173-05): the only guard is the in-transaction location check of the ledger (K-13 sabotage target).
    /// </summary>
    [Fact]
    public async Task AC13_a_reversal_into_a_location_that_became_inactive_is_refused_inside_the_posting_transaction()
    {
        var s = new InventoryScenario(_factory);
        var api = new LocationApi(s);
        await s.PostExpectingAsync(s.Receipt("5"), 201);
        var issueBody = s.Issue("5");
        var issue = await s.PostExpectingAsync(issueBody, 201);
        await api.Expect(api.StatusAsync(s.LocationA, "Inactive", "W/\"1\""), 200);

        var refused = await s.PostExpectingAsync(s.Reversal(issueBody, issue.GetProperty("transactionId").GetString()!), 422);

        Assert.Equal("UNKNOWN_LOCATION", refused.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(s.LocationA, refused.GetProperty("error").GetProperty("details").GetProperty("locationId").GetString());
        Assert.Equal(2, s.Persisted("inventory_transactions").Count);
        Assert.Equal((false, false), await api.Presence(s.LocationA));
    }

    [Fact]
    public async Task AC13_a_non_canonical_spelling_of_a_location_id_is_not_a_location()
    {
        var s = new InventoryScenario(_factory);
        var api = new LocationApi(s);

        var body = s.Receipt("1");
        body["toLocationId"] = s.LocationA.ToUpperInvariant();
        var refused = await s.PostExpectingAsync(body, 422);
        Assert.Equal("UNKNOWN_LOCATION", refused.GetProperty("error").GetProperty("code").GetString());
        await api.ExpectError(api.GetAsync($"/api/locations/{s.LocationA.ToUpperInvariant()}"), 404, "UNKNOWN_LOCATION");
    }

    [Fact]
    public async Task AC16_a_stale_If_Match_is_409_LOCATION_CONFLICT_and_a_missing_one_400()
    {
        var api = new LocationApi(new InventoryScenario(_factory));
        var site = await api.Node("Site", null);
        await api.Expect(api.PatchAsync(site.Id, new { name = "first" }, site.ETag), 200);

        await api.ExpectError(api.PatchAsync(site.Id, new { name = "second" }, site.ETag), 409, "LOCATION_CONFLICT");
        await api.ExpectError(api.StatusAsync(site.Id, "Inactive", site.ETag), 409, "LOCATION_CONFLICT");
        await api.ExpectError(api.PatchAsync(site.Id, new { name = "third" }, "not-an-etag"), 409, "LOCATION_CONFLICT");
        await api.ExpectError(api.PatchAsync(site.Id, new { name = "fourth" }, null), 400, "INVALID_REQUEST");
        await api.ExpectError(api.StatusAsync(site.Id, "Inactive", null), 400, "INVALID_REQUEST");
        Assert.Equal("first", (await api.Get(site.Id)).GetProperty("name").GetString());
    }

    /// <summary>
    /// AC-16: per round, one fresh empty bin; a receipt into it and its deactivation are released together. Exactly
    /// one of the two may win: (201, 422 LOCATION_HAS_STOCK) or (200, 422 UNKNOWN_LOCATION). Afterwards no Inactive
    /// bin holds stock. The driver's transaction re-runs (Mongo WriteConflict) are counted and reported.
    /// </summary>
    [Fact]
    public async Task AC16_a_posting_racing_a_deactivation_never_leaves_stock_on_an_inactive_location()
    {
        const int rounds = 30;
        var s = new InventoryScenario(_factory);
        var api = new LocationApi(s);
        var retries = _factory.Services.GetRequiredService<TransactionRetryCounter>();
        var postingRetriesBefore = retries.Count(TransactionRetryCounter.Posting);
        var locationRetriesBefore = retries.Count(TransactionRetryCounter.Location);
        int postingWon = 0, deactivationWon = 0;

        for (var round = 0; round < rounds; round++)
        {
            var bin = s.AddLocation();
            var gate = new TaskCompletionSource();
            var posting = Task.Run(async () => { await gate.Task; return await s.PostAsync(s.Receipt("1", location: bin)); });
            // Jitter 0/4/8/12 ms on the deactivation so both orders (and the overlap between them) occur.
            var delay = round % 4 * 4;
            var deactivation = Task.Run(async () => { await gate.Task; await Task.Delay(delay); return await api.StatusAsync(bin, "Inactive", "W/\"1\""); });
            gate.SetResult();

            using var posted = await posting;
            using var deactivated = await deactivation;
            var postStatus = (int)posted.StatusCode;
            var statusStatus = (int)deactivated.StatusCode;
            var postCode = postStatus == 422 ? (await LocationApi.Read(posted)).GetProperty("error").GetProperty("code").GetString() : null;
            var statusCode = statusStatus == 422 ? (await LocationApi.Read(deactivated)).GetProperty("error").GetProperty("code").GetString() : null;

            if (postStatus == 201)
            {
                Assert.True(statusStatus == 422 && statusCode == "LOCATION_HAS_STOCK", $"round {round}: posting 201 but deactivation {statusStatus} {statusCode}");
                postingWon++;
            }
            else
            {
                Assert.True(postStatus == 422 && postCode == "UNKNOWN_LOCATION", $"round {round}: posting {postStatus} {postCode}");
                Assert.Equal(200, statusStatus);
                deactivationWon++;
            }

            var node = await api.Get(bin);
            var presence = await api.Presence(bin);
            Assert.False(node.GetProperty("status").GetString() == "Inactive" && presence.HasStock, $"round {round}: Inactive bin holds stock");
        }

        var postingRetries = retries.Count(TransactionRetryCounter.Posting) - postingRetriesBefore;
        var locationRetries = retries.Count(TransactionRetryCounter.Location) - locationRetriesBefore;
        _output.WriteLine($"AC-16 race: rounds={rounds} postingWon={postingWon} deactivationWon={deactivationWon} " +
                          $"postingTxnReRuns={postingRetries} locationTxnReRuns={locationRetries}");
        Assert.Equal(rounds, postingWon + deactivationWon);
    }
}
