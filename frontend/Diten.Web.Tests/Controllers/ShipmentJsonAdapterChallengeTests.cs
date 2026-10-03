using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Diten.Web.Tests.Controllers;

public sealed class ShipmentJsonAdapterChallengeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ShipmentJsonAdapterChallengeTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Unauthenticated_adapter_uses_json_401_while_page_keeps_login_redirect()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var id = "44444444-4444-4444-8444-444444444444";
        var adapters = new[]
        {
            new HttpRequestMessage(HttpMethod.Get, "/SupplyChain/Shipments/api"),
            new HttpRequestMessage(HttpMethod.Get, $"/SupplyChain/Shipments/api/{id}"),
            JsonPost("/SupplyChain/Shipments/api"),
            JsonPost($"/SupplyChain/Shipments/api/{id}/transition"),
            JsonPost($"/SupplyChain/Shipments/api/{id}/pod")
        };

        foreach (var request in adapters)
        {
            using (request)
            using (var adapter = await client.SendAsync(request))
            {
                Assert.Equal(HttpStatusCode.Unauthorized, adapter.StatusCode);
                Assert.Null(adapter.Headers.Location);
                Assert.True(adapter.Headers.Contains("X-Correlation-Id"));
                using var body = JsonDocument.Parse(await adapter.Content.ReadAsStringAsync());
                Assert.Equal("INVALID_REQUEST", body.RootElement.GetProperty("error").GetProperty("code").GetString());
                Assert.Equal("v1", body.RootElement.GetProperty("contractVersion").GetString());
            }
        }

        var page = await client.GetAsync("/SupplyChain/Shipments");
        Assert.Equal(HttpStatusCode.Found, page.StatusCode);
        Assert.Equal("/account/login", page.Headers.Location?.AbsolutePath);
    }

    private static HttpRequestMessage JsonPost(string path) => new(HttpMethod.Post, path)
    {
        Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
    };
}
