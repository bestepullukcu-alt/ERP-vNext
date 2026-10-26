using System.Net;
using System.Text.Json;
using Diten.PlanningService.Api.Features.Mrp;
using Diten.PlanningService.Api.Features.DemandPlanning;
using Diten.PlanningService.Application.Features.Mrp;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Diten.PlanningService.Cycles.Tests;

public sealed class MrpUpstreamReaderTests
{
    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("AAAAAAAA-AAAA-4AAA-8AAA-AAAAAAAAAAAA")]
    public void PlanningCorrelation_RejectsInvalidInboundIdentity(string value)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-Id"] = value;
        Assert.False(PlanningCorrelation.IsValid(context));
    }

    [Fact]
    public void PlanningCorrelation_RejectsMultipleValuesAndAcceptsCanonicalGuid()
    {
        var context = new DefaultHttpContext();
        var id = Guid.NewGuid().ToString("D");
        context.Request.Headers["X-Correlation-Id"] = id;
        Assert.True(PlanningCorrelation.IsValid(context));
        context.Request.Headers["X-Correlation-Id"] = new[] { id, Guid.NewGuid().ToString("D") };
        Assert.False(PlanningCorrelation.IsValid(context));
    }

    [Fact]
    public async Task MdmReader_ValidatesActiveCompanyAndListsOnlyActive()
    {
        var tenant = Guid.NewGuid();
        var company = Guid.NewGuid();
        var other = Guid.NewGuid();
        var context = Context(tenant);
        var correlation = Guid.NewGuid().ToString("D");
        context.Request.Headers["X-Correlation-Id"] = correlation;
        var reader = new MdmLegalEntityScopeReader(new Clients(request =>
        {
            Assert.Equal(correlation, request.Headers.GetValues("X-Correlation-Id").Single());
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
        var correlation = Guid.NewGuid().ToString("D");
        context.HttpContext!.Request.Headers["X-Correlation-Id"] = correlation;
        var reader = new InventoryAvailabilityReader(new Clients(request =>
        {
            Assert.Equal(correlation, request.Headers.GetValues("X-Correlation-Id").Single());
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

    [Fact]
    public async Task SafetyStockReader_ForwardsScopeAndReadsVersionedEffectiveQuantity()
    {
        var tenant = Guid.NewGuid();
        var company = Guid.NewGuid();
        var sku = Guid.NewGuid();
        var warehouse = Guid.NewGuid();
        var revision = Guid.NewGuid();
        var context = Context(tenant);
        var correlation = Guid.NewGuid().ToString("D");
        context.Request.Headers["X-Correlation-Id"] = correlation;
        var reader = new SafetyStockQuantityReader(new Clients(request =>
        {
            Assert.Equal(correlation, request.Headers.GetValues("X-Correlation-Id").Single());
            Assert.Equal("Bearer fixture", request.Headers.Authorization?.ToString());
            Assert.Equal(tenant.ToString("D"), request.Headers.GetValues("X-Tenant-Id").Single());
            Assert.Equal(company.ToString("D"), request.Headers.GetValues("X-Legal-Entity-Id").Single());
            Assert.Contains($"skuId={sku:D}", request.RequestUri!.Query);
            Assert.Contains($"warehouseId={warehouse:D}", request.RequestUri.Query);
            Assert.Contains("asOf=2026-10-19", request.RequestUri.Query);
            return Json(HttpStatusCode.OK, JsonSerializer.Serialize(new { data = new {
                quantity = 2.5m, baseUomId = "EA", source = "Manual",
                policyRevisionId = revision, effectiveFrom = "2026-10-19",
                effectiveUntil = "2026-10-25", recordVersion = 3 } }));
        }), new HttpContextAccessor { HttpContext = context });

        var result = await reader.ReadAsync(tenant, company, sku, warehouse,
            new DateOnly(2026, 10, 19), CancellationToken.None);
        Assert.Equal(MrpSafetyStockStatus.Found, result.Status);
        Assert.Equal(2.5m, result.Value!.Quantity);
        Assert.Equal(revision, result.Value.PolicyRevisionId);
        Assert.Equal(3, result.Value.RecordVersion);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, MrpSafetyStockStatus.NotFound)]
    [InlineData(HttpStatusCode.Forbidden, MrpSafetyStockStatus.Unavailable)]
    [InlineData(HttpStatusCode.ServiceUnavailable, MrpSafetyStockStatus.Unavailable)]
    public async Task SafetyStockReader_DistinguishesAbsentRecordFromUnavailableAuthority(
        HttpStatusCode status, MrpSafetyStockStatus expected)
    {
        var tenant = Guid.NewGuid();
        var reader = new SafetyStockQuantityReader(new Clients(_ => Json(status, "{}")),
            new HttpContextAccessor { HttpContext = Context(tenant) });
        var result = await reader.ReadAsync(tenant, Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), new DateOnly(2026, 10, 19), CancellationToken.None);
        Assert.Equal(expected, result.Status);
    }

    [Fact]
    public async Task SafetyStockReader_RejectsMalformedSuccessAndWrongTenant()
    {
        var tenant = Guid.NewGuid();
        var reader = new SafetyStockQuantityReader(new Clients(_ => Json(HttpStatusCode.OK,
            "{\"data\":{\"quantity\":-1}}")),
            new HttpContextAccessor { HttpContext = Context(tenant) });
        var company = Guid.NewGuid();
        var sku = Guid.NewGuid();
        var warehouse = Guid.NewGuid();
        var asOf = new DateOnly(2026, 10, 19);

        Assert.Equal(MrpSafetyStockStatus.Unavailable,
            (await reader.ReadAsync(tenant, company, sku, warehouse, asOf,
                CancellationToken.None)).Status);
        Assert.Equal(MrpSafetyStockStatus.Unavailable,
            (await reader.ReadAsync(Guid.NewGuid(), company, sku, warehouse, asOf,
                CancellationToken.None)).Status);
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
