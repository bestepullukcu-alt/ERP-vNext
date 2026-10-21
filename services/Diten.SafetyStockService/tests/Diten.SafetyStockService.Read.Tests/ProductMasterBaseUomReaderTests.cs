using System.Net;
using Diten.SafetyStockService.Infrastructure;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Diten.SafetyStockService.Read.Tests;

public sealed class ProductMasterBaseUomReaderTests
{
    [Theory]
    [InlineData("Active", "EA", "EA")]
    [InlineData("Deactivated", "EA", null)]
    [InlineData("Active", null, null)]
    public async Task GetSku_RequiresActiveSkuAndBaseUnit(string state, string? unit, string? expected)
    {
        var sku = Guid.NewGuid();
        var context = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        context.HttpContext.Request.Headers.Authorization = "Bearer test-token";
        var handler = new StubHandler(request =>
        {
            Assert.Equal($"/api/product-master/skus/{sku:D}", request.RequestUri?.AbsolutePath);
            Assert.Equal("Bearer test-token", request.Headers.Authorization?.ToString());
            return new(HttpStatusCode.OK)
            {
                Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(new
                {
                    skuId = sku, lifecycleStatus = state, baseUomId = unit
                }))
            };
        });
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5059/") };
        var reader = new ProductMasterBaseUomReader(client, context);
        Assert.Equal(expected, await reader.ReadAsync(sku, default));
    }

    [Fact]
    public async Task MissingCallerToken_DoesNotCallMdm()
    {
        var handler = new StubHandler(_ => throw new Xunit.Sdk.XunitException("MDM should not be called"));
        var reader = new ProductMasterBaseUomReader(new HttpClient(handler),
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() });
        await Assert.ThrowsAsync<InvalidOperationException>(() => reader.ReadAsync(Guid.NewGuid(), default));
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(answer(request));
    }
}
