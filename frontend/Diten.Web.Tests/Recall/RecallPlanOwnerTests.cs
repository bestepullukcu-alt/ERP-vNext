using System.Net;
using System.Text;
using System.Text.Json;
using Diten.Web.Models.Quarantine;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Diten.Web.Tests.Recall;

/// <summary>
/// WP-MVP1-FU-CLEAN1 — FU-0177-PLAN-OWNER: the plan owner picker. The door /Recall/lookups/users (recall.drills.manage — the gate table)
/// asks AuthService's existing tenant-scoped lookup with the CALLER's token and the caller's tenant; the path carries only the search and the
/// limit, so a query string naming another tenant (or a bigger limit) changes nothing. Only uuid ids with a label become options.
/// </summary>
public sealed class RecallPlanOwnerTests
{
    private const string CallerTenant = "6f1d2c3b-4a59-4e6f-8a7b-9c0d1e2f3a4b"; // RecallDataGateTests.Build's signed-in user
    private const string Ayse = "aaaaaaaa-1111-4111-8111-000000000001";
    private const string Mert = "bbbbbbbb-2222-4222-8222-000000000002";

    [Fact]
    public async Task The_search_goes_to_the_tenant_lookup_with_the_callers_token_and_tenant_only()
    {
        var seen = new List<HttpRequestMessage>();
        var gateway = new RecallDataGateTests.FakeGateway
        {
            Answer = request =>
            {
                seen.Add(request);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(new
                    {
                        isSuccessful = true, statusCode = 200,
                        data = new object[]
                        {
                            new { userId = Ayse, displayLabel = "Ayşe Demir" }, new { userId = "not-a-uuid", displayLabel = "Kimse" },
                            new { userId = Mert, displayLabel = "  " }, new { userId = Ayse.ToUpperInvariant(), displayLabel = "Ayşe Demir" }
                        }
                    }), Encoding.UTF8, "application/json")
                };
            }
        };
        var controller = RecallDataGateTests.Build(gateway, [Diten.Web.Controllers.RecallKeys.DrillsManage]);
        // a query naming another tenant and a bigger limit: neither reaches the service
        controller.ControllerContext.HttpContext.Request.QueryString = new QueryString("?q=Ay%C5%9Fe&tenantId=99999999-9999-4999-8999-999999999999&limit=50");

        var result = await controller.UserLookup("Ayşe", CancellationToken.None);

        var request = Assert.Single(seen);
        Assert.Equal("/api/users/lookup?search=Ay%C5%9Fe&limit=20", request.RequestUri!.PathAndQuery);
        Assert.Equal("Bearer caller-token", request.Headers.Authorization?.ToString());
        Assert.Equal(CallerTenant, Assert.Single(request.Headers.GetValues("X-Tenant-Id")));
        Assert.DoesNotContain("99999999", request.RequestUri!.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(request.Headers, h => h.Value.Any(v => v.Contains("99999999", StringComparison.Ordinal)));

        var items = JsonDocument.Parse(((ContentResult)result).Content!).RootElement.GetProperty("items");
        var only = Assert.Single(items.EnumerateArray());
        Assert.Equal(Ayse, only.GetProperty("value").GetString());
        Assert.Equal("Ayşe Demir", only.GetProperty("text").GetString());
    }

    [Theory]
    [InlineData(null, "/api/users/lookup?search=&limit=20")]
    [InlineData("  Mert  ", "/api/users/lookup?search=Mert&limit=20")]
    [InlineData("a&tenantId=x", "/api/users/lookup?search=a%26tenantId%3Dx&limit=20")]
    public void The_search_path_is_the_text_and_the_limit_only(string? search, string expected)
    {
        Assert.Equal(expected, QuarantinePeople.SearchPath(search));
        Assert.Equal(QuarantinePeople.MaxSearchLength, Uri.UnescapeDataString(QuarantinePeople.SearchPath(new string('x', 300)).Split("search=")[1].Split('&')[0]).Length);
    }

    [Fact]
    public async Task A_refusal_of_the_lookup_is_the_pages_error_not_an_empty_list()
    {
        var gateway = new RecallDataGateTests.FakeGateway { Answer = _ => new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = new StringContent("{}") } };
        var result = await RecallDataGateTests.Build(gateway, [Diten.Web.Controllers.RecallKeys.DrillsManage]).UserLookup("a", CancellationToken.None);
        Assert.Equal(403, RecallDataGateTests.Status(result));
    }
}
