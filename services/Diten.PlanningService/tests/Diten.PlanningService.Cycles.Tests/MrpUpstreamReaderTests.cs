using System.Net;
using System.Text.Json;
using Diten.PlanningService.Api.Features.Mrp;
using Diten.PlanningService.Application.Features.Mrp;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Diten.PlanningService.Cycles.Tests;

public sealed class MrpUpstreamReaderTests
{
    [Fact]
    public async Task MdmReader_ValidatesActiveCompanyAndListsOnlyActive()
    {
        var tenant = Guid.NewGuid();
        var company = Guid.NewGuid();
        var other = Guid.NewGuid();
        var context = Context(tenant);
        var reader = new MdmLegalEntityScopeReader(new Clients(request =>
        {
            Assert.Equal("Bearer fixture", request.Headers.Authorization?.ToString());
            Assert.Equal(tenant.ToString("D"), request.Headers.GetValues("X-Tenant-Id").Single());
            var json = request.RequestUri!.AbsolutePath.EndsWith("lookup-validation")
                ? JsonSerializer.Serialize(new { data = new { legalEntityId = company, referenceable = true } })
                : JsonSerializer.Serialize(new { data = new[] {
                    new { legalEntityId = company, referenceable = true },
                    new { legalEntityId = other, referenceable = false } } });
            return Json(HttpStatusCode.OK, json);
        }), new HttpContextAccessor { HttpContext = context });
        Assert.Equal(LegalEntityReferenceResult.Active,
            await reader.ValidateAsync(tenant, company, CancellationToken.None));
        Assert.Equal([company], await reader.ListActiveAsync(tenant, CancellationToken.None));
    }

    [Fact]
    public async Task MdmReader_FailsClosedOnUnavailableOrWrongCompany()
    {
        var tenant = Guid.NewGuid();
        var company = Guid.NewGuid();
        var context = new HttpContextAccessor { HttpContext = Context(tenant) };
        var unavailable = new MdmLegalEntityScopeReader(new Clients(_ =>
            Json(HttpStatusCode.ServiceUnavailable, "{}")), context);
        Assert.Equal(LegalEntityReferenceResult.Unavailable,
            await unavailable.ValidateAsync(tenant, company, CancellationToken.None));
        var wrong = new MdmLegalEntityScopeReader(new Clients(_ => Json(HttpStatusCode.OK,
            JsonSerializer.Serialize(new { data = new { legalEntityId = Guid.NewGuid(), referenceable = true } }))), context);
        Assert.Equal(LegalEntityReferenceResult.NotFound,
            await wrong.ValidateAsync(tenant, company, CancellationToken.None));
    }

    [Fact]
    public async Task InventoryReader_RequiresMatchingScopeUnitAndFreshWireShape()
    {
        var tenant = Guid.NewGuid();
        var company = Guid.NewGuid();
        var sku = Guid.NewGuid();
        var warehouse = Guid.NewGuid();
        var context = new HttpContextAccessor { HttpContext = Context(tenant) };
        var reader = new InventoryAvailabilityReader(new Clients(request =>
        {
            Assert.Equal(company.ToString("D"),
                request.Headers.GetValues("X-Legal-Entity-Id").Single());
            return Json(HttpStatusCode.OK, JsonSerializer.Serialize(new {
                skuId = sku, legalEntityId = company,
                scope = new { warehouseId = warehouse }, baseUomId = "EA",
                available = "12.5", asOf = "2026-10-19T12:00:00Z" }));
        }), context);
        var stock = await reader.ReadAsync(tenant, company, sku, warehouse, CancellationToken.None);
        Assert.NotNull(stock);
        Assert.Equal(12.5m, stock.Available);
        Assert.Equal("EA", stock.BaseUomId);
        Assert.Null(await reader.ReadAsync(tenant, company, sku, Guid.NewGuid(), CancellationToken.None));
    }

    private static DefaultHttpContext Context(Guid tenant)
    {
        var context = new DefaultHttpContext();
        context.Items["DemandPlanning.TenantId"] = tenant;
        context.Request.Headers.Authorization = "Bearer fixture";
        return context;
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

    private sealed class Clients(Func<HttpRequestMessage, HttpResponseMessage> reply) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new Handler(reply))
        {
            BaseAddress = new Uri("http://127.0.0.1:5000/")
        };
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> reply) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken ct) => Task.FromResult(reply(request));
    }
}
