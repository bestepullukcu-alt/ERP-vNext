using System.Net;
using System.Text.Json;
using Diten.DevEnablementService.Domain.Entities;
using Xunit;
using Xunit.Abstractions;

namespace Diten.DevEnablementService.Api.Tests.ServerList;

/// <summary>
/// THE SERVER-MODE LIST CONTRACT, OVER HTTP, ON A REAL MONGO (WP-UI-LIST-SERVER-01, BL-440 package 3).
///
/// Golden Compact is the server-mode reference: `GET /api/golden-reference-compact` with list parameters answers
/// `data = { items, total, filteredTotal }`. What is measured here is exactly what a wrong implementation hides:
/// a page that silently drops rows (paging walks every row exactly once), a sort that is a no-op, a search that is
/// case-sensitive or treats the text as a pattern, a filter that is ignored, a count that includes another tenant or
/// the soft-deleted, an `orderBy` that reaches Mongo unchecked. The parameterless call keeps its old array shape.
///
/// Data (per run, fresh tenants): tenant A has 30 live rows GRC-001…GRC-030 and one SOFT-DELETED row; tenant B has 5
/// rows, one of which reuses code GRC-001 and all of which carry the word "Foreign" in the name.
/// </summary>
[Collection(ServerListCollection.Name)]
public sealed class GoldenCompactServerListTests : IAsyncLifetime
{
    private const string Endpoint = "/api/golden-reference-compact";
    private const string Read = "goldencompact.records.read";
    private static readonly string[] Types = ["Standard", "Custom", "Pro"];

    private readonly DevEnablementTestHost _host;
    private readonly ITestOutputHelper _output;
    private static readonly SemaphoreSlim SeedLock = new(1, 1);
    private static bool _seeded;

    public GoldenCompactServerListTests(DevEnablementTestHost host, ITestOutputHelper output)
    {
        _host = host;
        _output = output;
    }

    public async Task InitializeAsync()
    {
        await SeedLock.WaitAsync();
        try
        {
            if (_seeded) return;
            var rows = new List<GoldenReferenceCompact>();
            for (var i = 1; i <= 30; i++)
            {
                rows.Add(new GoldenReferenceCompact
                {
                    TenantId = _host.TenantA,
                    Code = $"GRC-{i:000}",
                    Name = $"Reference {(char)('A' + (i - 1) % 26)}{i:000}",
                    ReferenceType = Types[i % 3],
                    Category = i % 2 == 0 ? "Alpha" : "Beta",
                    Owner = i <= 10 ? "Ops" : "Finance",
                    Version = $"1.{i % 4}",
                    Priority = (i % 5) * 10 + 10,        // 10…50, six rows each → ties the sort must break stably
                    IsActive = i % 2 == 1                  // 15 active, 15 passive
                });
            }

            rows.Add(new GoldenReferenceCompact { TenantId = _host.TenantA, Code = "GRC-DEL", Name = "Deleted row", IsDeleted = true, DeletedAt = DateTimeOffset.UtcNow, Priority = 70 });
            for (var i = 1; i <= 5; i++)
            {
                rows.Add(new GoldenReferenceCompact
                {
                    TenantId = _host.TenantB,
                    Code = i == 1 ? "GRC-001" : $"FRN-{i:000}",
                    Name = $"Foreign secret {i}",
                    ReferenceType = "Pro",
                    Category = "Alpha",
                    Owner = "Ops",
                    Priority = 70,
                    IsActive = true
                });
            }

            await _host.Collection.InsertManyAsync(rows);
            _seeded = true;
        }
        finally
        {
            SeedLock.Release();
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ── paging ───────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_page_returns_length_rows_and_both_totals()
    {
        var page = await List(_host.TenantA, "start=0&length=10&orderBy=code&orderDir=asc");

        Assert.Equal(Codes(1, 10), page.Codes);
        Assert.Equal(30, page.Total);
        Assert.Equal(30, page.FilteredTotal);

        var last = await List(_host.TenantA, "start=20&length=10&orderBy=code&orderDir=asc");
        Assert.Equal(Codes(21, 30), last.Codes);
    }

    [Fact]
    public async Task A_real_exchange_is_written_to_the_test_output()
    {
        // Evidence for the work-package report: one request exactly as the factory builds it, and the wire answer.
        const string query = "start=0&length=2&search=grc&orderBy=priority&orderDir=desc&draw=4&status=Active&status=Passive&referenceType=Pro";
        using var client = _host.Client(_host.TokenFor(_host.TenantA, Read));
        var response = await client.GetAsync($"{Endpoint}?{query}");
        var body = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"GET {Endpoint}?{query}");
        _output.WriteLine($"→ {(int)response.StatusCode} {body}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(body);
        Assert.Equal(10, json.RootElement.GetProperty("data").GetProperty("filteredTotal").GetInt64());
    }

    [Fact]
    public async Task Walking_every_page_sees_every_row_exactly_once_even_on_a_sort_with_ties()
    {
        // Priority has six rows per value: without a tie-breaker two page requests may order the ties differently.
        var seen = new List<string>();
        for (var start = 0; start < 30; start += 7)
        {
            var page = await List(_host.TenantA, $"start={start}&length=7&orderBy=priority&orderDir=desc");
            Assert.Equal(30, page.FilteredTotal);
            seen.AddRange(page.Codes);
        }

        Assert.Equal(30, seen.Count);
        Assert.Equal(Codes(1, 30), seen.OrderBy(c => c, StringComparer.Ordinal).ToList());
    }

    // ── sorting ──────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task OrderBy_and_orderDir_sort_on_the_server()
    {
        var byPriority = await List(_host.TenantA, "start=0&length=30&orderBy=priority&orderDir=desc");
        var priorities = byPriority.Items.Select(x => x.GetProperty("priority").GetInt32()).ToList();
        Assert.Equal(priorities.OrderByDescending(p => p).ToList(), priorities);
        Assert.Equal(50, priorities[0]);

        var byCodeDesc = await List(_host.TenantA, "start=0&length=3&orderBy=code&orderDir=desc");
        Assert.Equal(["GRC-030", "GRC-029", "GRC-028"], byCodeDesc.Codes);
    }

    [Theory]
    [InlineData("orderBy=tenantId")]
    [InlineData("orderBy=isDeleted")]
    [InlineData("orderBy=description")]
    [InlineData("orderBy=code;drop")]
    public async Task Unknown_orderBy_is_refused_400(string query)
    {
        using var client = _host.Client(_host.TokenFor(_host.TenantA, Read));
        var response = await client.GetAsync($"{Endpoint}?start=0&length=10&{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("orderBy", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("orderBy=code&orderDir=sideways")]
    [InlineData("start=-1&length=10")]
    [InlineData("start=0&length=0")]
    [InlineData("start=0&length=501")]
    [InlineData("status=Maybe")]
    public async Task Malformed_list_parameters_are_refused_400(string query)
    {
        using var client = _host.Client(_host.TokenFor(_host.TenantA, Read));
        var response = await client.GetAsync($"{Endpoint}?{query}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── search ───────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Search_is_case_insensitive_on_code_and_name_and_narrows_filteredTotal_only()
    {
        var byCode = await List(_host.TenantA, "start=0&length=50&search=grc-00");
        Assert.Equal(Codes(1, 9), byCode.Codes.OrderBy(c => c, StringComparer.Ordinal).ToList());
        Assert.Equal(9, byCode.FilteredTotal);
        Assert.Equal(30, byCode.Total);

        var byName = await List(_host.TenantA, "start=0&length=50&search=REFERENCE%20C003");
        Assert.Equal(["GRC-003"], byName.Codes);
    }

    [Fact]
    public async Task Search_text_is_never_a_pattern()
    {
        // "." would match every row as a regex; as text it matches none (no code or name contains a dot).
        var dot = await List(_host.TenantA, "start=0&length=50&search=.");
        Assert.Empty(dot.Codes);
        Assert.Equal(0, dot.FilteredTotal);
        Assert.Equal(30, dot.Total);
    }

    // ── filters ──────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Multi_filters_arrive_as_repeated_parameters_and_single_ones_once()
    {
        var active = await List(_host.TenantA, "start=0&length=50&status=Active");
        Assert.Equal(15, active.FilteredTotal);
        Assert.All(active.Items, x => Assert.True(x.GetProperty("isActive").GetBoolean()));

        var both = await List(_host.TenantA, "start=0&length=50&status=Active&status=Passive");
        Assert.Equal(30, both.FilteredTotal);

        var types = await List(_host.TenantA, "start=0&length=50&referenceType=Pro&referenceType=Custom");
        Assert.Equal(20, types.FilteredTotal);
        Assert.All(types.Items, x => Assert.Contains(x.GetProperty("referenceType").GetString(), new[] { "Pro", "Custom" }));

        var priority = await List(_host.TenantA, "start=0&length=50&priority=30");
        Assert.Equal(6, priority.FilteredTotal);
        Assert.All(priority.Items, x => Assert.Equal(30, x.GetProperty("priority").GetInt32()));

        var combined = await List(_host.TenantA, "start=0&length=2&category=Alpha&owner=Ops&status=Passive&search=grc");
        Assert.Equal(5, combined.FilteredTotal);   // even i ≤ 10: 2,4,6,8,10
        Assert.Equal(2, combined.Codes.Count);     // a page of them — filteredTotal > rows is paging, not loss
        Assert.Equal(30, combined.Total);
    }

    // ── the tenant boundary ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task No_row_or_count_ever_crosses_the_tenant_boundary()
    {
        var a = await List(_host.TenantA, "start=0&length=500&search=foreign");
        Assert.Empty(a.Codes);
        Assert.Equal(30, a.Total);

        var shared = await List(_host.TenantA, "start=0&length=500&search=GRC-001");
        Assert.Equal(["GRC-001"], shared.Codes);
        Assert.DoesNotContain(shared.Items, x => x.GetProperty("name").GetString()!.Contains("Foreign"));

        var b = await List(_host.TenantB, "start=0&length=500&orderBy=code");
        Assert.Equal(5, b.Total);
        Assert.Equal(5, b.FilteredTotal);
        Assert.All(b.Items, x => Assert.StartsWith("Foreign secret", x.GetProperty("name").GetString()));

        var bPriority = await List(_host.TenantA, "start=0&length=500&priority=70");
        Assert.Equal(0, bPriority.FilteredTotal); // only B's rows (and A's deleted row) have priority 70
    }

    [Fact]
    public async Task Soft_deleted_rows_are_neither_listed_nor_counted()
    {
        var page = await List(_host.TenantA, "start=0&length=500&search=GRC-DEL");
        Assert.Empty(page.Codes);
        Assert.Equal(30, page.Total);
    }

    // ── the old shape, the gates ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_parameterless_call_keeps_the_whole_list_array_shape()
    {
        using var client = _host.Client(_host.TokenFor(_host.TenantA, Read));
        var response = await client.GetAsync(Endpoint);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var data = json.RootElement.GetProperty("data");
        Assert.Equal(JsonValueKind.Array, data.ValueKind);
        var codes = data.EnumerateArray().Select(x => x.GetProperty("code").GetString()!).ToList();
        Assert.Equal(Codes(1, 30), codes); // sorted by code, no tenant B, no deleted row, no paging
    }

    [Fact]
    public async Task The_list_still_needs_a_token_and_the_read_permission()
    {
        // No token and no tenant: the tenant middleware (it runs before authorization) refuses it 400 "Missing Tenant".
        using var anonymous = _host.Client(null);
        Assert.Equal(HttpStatusCode.BadRequest, (await anonymous.GetAsync($"{Endpoint}?start=0&length=10")).StatusCode);

        // No token, a tenant header: the tenant resolves, authorization refuses it.
        using var headerOnly = _host.Client(null);
        headerOnly.DefaultRequestHeaders.Add("X-Tenant-Id", _host.TenantA.ToString());
        Assert.Equal(HttpStatusCode.Unauthorized, (await headerOnly.GetAsync($"{Endpoint}?start=0&length=10")).StatusCode);

        using var noPermission = _host.Client(_host.TokenFor(_host.TenantA));
        Assert.Equal(HttpStatusCode.Forbidden, (await noPermission.GetAsync($"{Endpoint}?start=0&length=10")).StatusCode);
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────

    private sealed record Page(List<JsonElement> Items, long Total, long FilteredTotal)
    {
        public List<string> Codes => Items.Select(x => x.GetProperty("code").GetString()!).ToList();
    }

    private async Task<Page> List(Guid tenant, string query)
    {
        using var client = _host.Client(_host.TokenFor(tenant, Read));
        var response = await client.GetAsync($"{Endpoint}?{query}");
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{query} → {(int)response.StatusCode}: {body}");

        using var json = JsonDocument.Parse(body);
        var data = json.RootElement.GetProperty("data");
        return new Page(
            data.GetProperty("items").EnumerateArray().Select(x => x.Clone()).ToList(),
            data.GetProperty("total").GetInt64(),
            data.GetProperty("filteredTotal").GetInt64());
    }

    private static List<string> Codes(int from, int to) =>
        Enumerable.Range(from, to - from + 1).Select(i => $"GRC-{i:000}").ToList();
}
