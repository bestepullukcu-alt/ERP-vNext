using System.Net;
using System.Text;
using Diten.Web.Tests.Contracts;
using Xunit;

namespace Diten.Web.Tests.Recall;

/// <summary>
/// WP-MVP1-FU-CLEAN2 (FU-WEB-CONTRACT-FAKES-RECALL) — the binding of the Recall Web fakes to RECALL 1.9.0 works in both directions: an
/// off-contract member is refused, the bare placeholder may only miss required members, the placeholder takes the contract's shape (array /
/// object / file), a POST answer is measured against the POST operation only, and a deliberate negative passes only by its named violation.
/// </summary>
public sealed class RecallContractFakesTests
{
    private const string Run = "0b6c2f1e-3d4a-4c5b-9e8f-7a6b5c4d3e2f";

    private static HttpRequestMessage Get(string path) => new(HttpMethod.Get, "http://gateway" + path);

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private const string Drill = """
        {"runId":"0b6c2f1e-3d4a-4c5b-9e8f-7a6b5c4d3e2f","number":"RC-2026-D001","planId":"00000007-0000-4000-8000-000000000007","status":"Completed",
         "selectionMode":"Manual","lot":{"lotId":"l1","lotNumber":"L-1"},"startedAtUtc":"2026-10-15T08:00:00Z","targetHours":4,"version":3,
         "startedBy":{"userId":"00000004-0000-4000-8000-000000000004","name":"Ayşe Demir"},
         "steps":[{"step":"Trace","completedAtUtc":"2026-10-15T08:10:00Z","completedBy":{"userId":"00000004-0000-4000-8000-000000000004","name":"Ayşe Demir"}}],
         "completed":{"userId":"00000004-0000-4000-8000-000000000004","atUtc":"2026-10-15T09:00:00Z","name":"Ayşe Demir"}}
        """;

    [Fact]
    public void A_whole_1_9_0_drill_run_with_its_named_people_is_bound()
        => RecallContractFakes.Bound(Get($"/api/recall/drill-runs/{Run}"), Json(Drill));

    [Fact]
    public void A_person_member_off_the_1_9_0_UserRef_is_refused()
    {
        var off = Drill.Replace("\"name\":\"Ayşe Demir\"}}]", "\"name\":\"Ayşe Demir\",\"login\":\"ayse\"}}]", StringComparison.Ordinal);
        Assert.NotEqual(Drill, off);
        var ex = Assert.Throws<ContractViolationException>(() => RecallContractFakes.Bound(Get($"/api/recall/drill-runs/{Run}"), Json(off)));
        Assert.Contains(ex.Errors, e => e.Contains("/steps/0/completedBy/login", StringComparison.Ordinal));
    }

    [Fact]
    public void The_bare_placeholder_may_miss_members_but_never_carry_an_unknown_one()
    {
        RecallContractFakes.Bound(Get($"/api/recall/drill-runs/{Run}"), Json("{}"));
        Assert.Throws<ContractViolationException>(() => RecallContractFakes.Bound(Get("/api/recall/cases"), Json("{\"unknown\":1}")));
    }

    [Theory]
    [InlineData("/api/recall/drill-plans", "[]")]
    [InlineData($"/api/recall/cases/{Run}/stock", "[]")]
    [InlineData($"/api/recall/drill-runs/{Run}", "{}")]
    public async Task The_placeholder_takes_the_contracts_shape(string path, string expected)
    {
        using var response = RecallContractFakes.Placeholder(Get(path));
        Assert.Equal(expected, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public void A_download_route_gets_an_empty_file_placeholder_not_json()
    {
        using var response = RecallContractFakes.Placeholder(Get($"/api/recall/evidence/exports/{Run}/file"));
        Assert.Equal("application/octet-stream", response.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public void A_POST_answer_is_measured_against_the_POST_operation_only()
    {
        // a drill-plan LIST (the GET answer) is not a valid answer of createDrillPlan
        var post = new HttpRequestMessage(HttpMethod.Post, "http://gateway/api/recall/drill-plans");
        Assert.Throws<ContractViolationException>(() => RecallContractFakes.Bound(post, Json("[]", HttpStatusCode.Created)));
    }

    [Fact]
    public void A_deliberate_negative_passes_only_by_its_named_violation()
    {
        var off = Drill.Replace("\"status\":\"Completed\"", "\"status\":\"Paused\"", StringComparison.Ordinal);
        RecallContractFakes.Bound(Get($"/api/recall/drill-runs/{Run}"), RecallContractFakes.Off(Json(off), "/status"));
        Assert.Throws<ContractViolationException>(() => RecallContractFakes.Bound(Get($"/api/recall/drill-runs/{Run}"), RecallContractFakes.Off(Json(Drill), "/status")));
    }
}
