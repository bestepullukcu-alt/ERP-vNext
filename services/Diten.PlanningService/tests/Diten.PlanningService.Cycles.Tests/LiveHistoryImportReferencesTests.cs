using System.Net;
using System.Text;
using System.Text.Json;
using Diten.PlanningService.Api.Features.DemandPlanning;
using Diten.PlanningService.Application.Features.DemandPlanning.HistoryImports;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Diten.PlanningService.Cycles.Tests;

public sealed class LiveHistoryImportReferencesTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid Actor = Guid.NewGuid();
    private static readonly Guid LegalEntity = Guid.NewGuid();
    private static readonly Guid Sku = Guid.NewGuid();

    [Fact]
    public async Task ActiveCompanyWarehouseAndSku_AreVerifiedButUnitIsPending()
    {
        var reader = Create(path => path switch
        {
            var p when p.Contains("lookup-validation") =>
                Ok(new { data = new { legalEntityId = LegalEntity, referenceable = true } }),
            var p when p.Contains("api/locations/") => Warehouse(),
            var p when p.Contains("api/gskus/") =>
                Ok(new { data = new { id = Sku, lifecycleStatus = 3, packUomCode = "CASE" } }),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        });

        Assert.Equal(LegalEntity, await reader.ResolveSelectedAsync(Tenant, Actor, LegalEntity, default));
        Assert.True(await reader.IsAuthorizedAsync(Tenant, Actor, LegalEntity,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 2), ["wh-01"], default));
        var result = await reader.CheckAsync(Tenant, LegalEntity, Sku, "Gsku", "wh-01",
            new DateOnly(2026, 1, 1), "EA", default);
        Assert.Equal(HistoryReferenceState.Verified, result.State);
        Assert.True(result.UnitVerificationPending);
        Assert.Null(result.BaseUomId);
        Assert.Null(result.ConversionFactor);
    }

    [Fact]
    public async Task WrongCompanyWarehouse_IsInvalid()
    {
        var reader = Create(path => path.Contains("api/locations/") ?
            Ok(new { locationId = "wh-01", legalEntityId = Guid.NewGuid(),
                type = "Warehouse", status = "Active", isStockable = false }) :
            new HttpResponseMessage(HttpStatusCode.NotFound));
        var result = await reader.CheckAsync(Tenant, LegalEntity, Sku, "Gsku", "wh-01",
            new DateOnly(2026, 1, 1), "EA", default);
        Assert.Equal(HistoryReferenceState.Invalid, result.State);
    }

    [Fact]
    public async Task RetiredSku_IsInvalid()
    {
        var reader = Create(path => path.Contains("api/locations/") ? Warehouse() :
            Ok(new { data = new { id = Sku, lifecycleStatus = 4 } }));
        var result = await reader.CheckAsync(Tenant, LegalEntity, Sku, "Gsku", "wh-01",
            new DateOnly(2026, 1, 1), "EA", default);
        Assert.Equal(HistoryReferenceState.Invalid, result.State);
    }

    [Fact]
    public async Task UpstreamFailure_DoesNotBecomeVerified()
    {
        var reader = Create(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        await Assert.ThrowsAsync<InvalidOperationException>(() => reader.CheckAsync(
            Tenant, LegalEntity, Sku, "Gsku", "wh-01", new DateOnly(2026, 1, 1), "EA", default));
    }

    private static HttpResponseMessage Warehouse() => Ok(new {
        locationId = "wh-01", legalEntityId = LegalEntity, type = "Warehouse",
        status = "Active", isStockable = false });

    private static HttpResponseMessage Ok(object value) => new(HttpStatusCode.OK)
    { Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json") };

    private static LiveHistoryImportReferences Create(Func<string, HttpResponseMessage> response)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer test-token";
        var accessor = new HttpContextAccessor { HttpContext = context };
        var client = new HttpClient(new StubHandler(response))
        { BaseAddress = new Uri("http://127.0.0.1:5000/") };
        return new LiveHistoryImportReferences(new Factory(client), accessor);
    }

    private sealed class Factory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class StubHandler(Func<string, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Assert.Equal("Bearer test-token", request.Headers.Authorization?.ToString());
            return Task.FromResult(response(request.RequestUri?.AbsolutePath ?? string.Empty));
        }
    }
}
