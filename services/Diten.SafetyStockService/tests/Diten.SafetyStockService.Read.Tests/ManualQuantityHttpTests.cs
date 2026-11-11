using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Diten.SafetyStockService.Application;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Diten.SafetyStockService.Read.Tests;

public sealed class ManualQuantityHttpTests
{
    private static readonly string Secret = TestJwtSecret.Value;
    private static readonly Guid Sku = Guid.NewGuid();
    private static readonly Guid Warehouse = Guid.NewGuid();
    private static readonly Guid Policy = Guid.NewGuid();
    private static readonly Guid Revision = Guid.NewGuid();

    static ManualQuantityHttpTests() => Environment.SetEnvironmentVariable("JwtSettings__Secret", Secret);

    [Fact]
    public async Task EffectiveAmount_ReturnsContractFieldsAndEnforcesPermission()
    {
        using var factory = Factory(ManualQuantityStatus.Found, ReferenceCheck.Valid);
        using var client = factory.CreateClient();
        var path = Path();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
        using var wrong = Request(path, Token("safety-stock.evaluations.read"));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(wrong)).StatusCode);
        using var allowed = Request(path, Token("safety-stock.policies.read"));
        using var response = await client.SendAsync(allowed);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var value = json.RootElement.GetProperty("data");
        Assert.Equal(7m, value.GetProperty("quantity").GetDecimal());
        Assert.Equal("EA", value.GetProperty("baseUomId").GetString());
        Assert.Equal("Manual", value.GetProperty("source").GetString());
        Assert.Equal(Revision, value.GetProperty("policyRevisionId").GetGuid());
        Assert.Equal(3, value.GetProperty("recordVersion").GetInt64());
        Assert.Equal("2026-10-19", value.GetProperty("effectiveFrom").GetString());
        Assert.Equal("2026-10-25", value.GetProperty("effectiveUntil").GetString());
    }

    [Theory]
    [InlineData(ManualQuantityStatus.NotFound, ReferenceCheck.Valid, HttpStatusCode.NotFound)]
    [InlineData(ManualQuantityStatus.Unavailable, ReferenceCheck.Valid, HttpStatusCode.ServiceUnavailable)]
    [InlineData(ManualQuantityStatus.Found, ReferenceCheck.Unavailable, HttpStatusCode.ServiceUnavailable)]
    public async Task EffectiveAmount_SeparatesMissingFromUnavailable(
        ManualQuantityStatus state, ReferenceCheck company, HttpStatusCode expected)
    {
        using var factory = Factory(state, company);
        using var client = factory.CreateClient();
        using var request = Request(Path(), Token("safety-stock.policies.read"));
        Assert.Equal(expected, (await client.SendAsync(request)).StatusCode);
    }

    private static string Path() => $"/api/v1/safety-stock/policies/manual-quantities/effective" +
        $"?skuId={Sku:D}&warehouseId={Warehouse:D}&asOf=2026-10-21";

    private static WebApplicationFactory<Program> Factory(ManualQuantityStatus state, ReferenceCheck company) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ILegalEntityReferenceValidator>();
            services.RemoveAll<ISkuBaseUomReader>();
            services.RemoveAll<IManualQuantityStore>();
            services.AddSingleton<ILegalEntityReferenceValidator>(new StubCompany(company));
            services.AddSingleton<ISkuBaseUomReader>(new StubUom());
            services.AddSingleton<IManualQuantityStore>(new StubStore(state));
        }));

    private static HttpRequestMessage Request(string path, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("X-Legal-Entity-Id", Guid.NewGuid().ToString("D"));
        return request;
    }

    private static string Token(string permission) => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
        issuer: "Diten.AuthService", audience: "DitenERP",
        claims: [new Claim("sub", Guid.NewGuid().ToString("D")),
            new Claim("tenant_id", Guid.NewGuid().ToString("D")), new Claim("permission", permission)],
        notBefore: DateTime.UtcNow.AddMinutes(-1), expires: DateTime.UtcNow.AddMinutes(5),
        signingCredentials: new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)), SecurityAlgorithms.HmacSha256)));

    private sealed class StubCompany(ReferenceCheck status) : ILegalEntityReferenceValidator
    {
        public Task<ReferenceCheck> ValidateAsync(Guid company, CancellationToken ct) => Task.FromResult(status);
    }

    private sealed class StubUom : ISkuBaseUomReader
    {
        public Task<string?> ReadAsync(Guid sku, CancellationToken ct) => Task.FromResult<string?>("EA");
    }

    private sealed class StubStore(ManualQuantityStatus status) : IManualQuantityStore
    {
        public Task<ManualQuantityResult> ExecuteAsync(ManualQuantityCommand command,
            ActorSeriesScope scope, string uom, CancellationToken ct) => throw new NotSupportedException();

        public Task<ManualQuantityResult> ReadEffectiveAsync(EffectiveManualQuantityQuery query,
            ActorSeriesScope scope, string uom, CancellationToken ct)
        {
            Assert.Equal(new DateOnly(2026, 10, 21), query.AsOf);
            return Task.FromResult(new ManualQuantityResult(status, status == ManualQuantityStatus.Found ?
                new(Guid.NewGuid(), Policy, Revision, 7m, "EA", new DateOnly(2026, 10, 19),
                    new DateOnly(2026, 10, 25), "Approved", 3) : null));
        }
    }
}
