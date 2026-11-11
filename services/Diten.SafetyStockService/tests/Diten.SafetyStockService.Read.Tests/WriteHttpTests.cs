using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.SafetyStockService.Api.ModuleRegistration;
using Diten.SafetyStockService.Application;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Diten.SafetyStockService.Read.Tests;

internal static class TestJwtSecret
{
    internal static readonly string Value = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
}

public sealed class WriteHttpTests
{
    private static readonly string Secret = TestJwtSecret.Value;
    private const string Route = "/api/v1/safety-stock/policies";

    static WriteHttpTests() => Environment.SetEnvironmentVariable("JwtSettings__Secret", Secret);

    [Theory]
    [InlineData("create", "safety-stock.policies.create")]
    [InlineData("revise", "safety-stock.policies.update")]
    [InlineData("submit", "safety-stock.policies.submit")]
    [InlineData("approve", "safety-stock.policies.review")]
    [InlineData("reject", "safety-stock.policies.review")]
    public async Task Commands_EnforceExactPermissionAndCompanySelection(string operation, string permission)
    {
        using var factory = Factory(ReferenceCheck.Valid);
        using var client = factory.CreateClient();
        using var anonymous = Request(operation, null);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(anonymous)).StatusCode);

        using var wrongPermission = Request(operation, Token("safety-stock.policies.read"));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(wrongPermission)).StatusCode);

        using var authorized = Request(operation, Token(permission));
        Assert.Equal(HttpStatusCode.Created, (await client.SendAsync(authorized)).StatusCode);

        using var missingCompany = Request(operation, Token(permission), company: false);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(missingCompany)).StatusCode);
    }

    [Fact]
    public async Task MdmUnavailable_FailsClosedBeforeStore()
    {
        using var factory = Factory(ReferenceCheck.Unavailable);
        using var client = factory.CreateClient();
        using var request = Request("create", Token("safety-stock.policies.create"));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.SendAsync(request)).StatusCode);
    }

    [Fact]
    public void Manifest_DeclaresAllEnforcedApiOnlyPermissions()
    {
        var manifest = new SafetyStockManifestProvider().GetManifest();
        var keys = manifest.ApiPermissions!.Select(p => p.PermissionKey).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(new[]
        {
            "safety-stock.evaluations.read", "safety-stock.policies.create", "safety-stock.policies.read",
            "safety-stock.policies.review", "safety-stock.policies.submit", "safety-stock.policies.update"
        }, keys.OrderBy(key => key, StringComparer.Ordinal));
        Assert.All(manifest.ApiPermissions!, permission => Assert.Equal("Tenant", permission.Scope));
        Assert.Empty(manifest.Pages);
    }

    private static WebApplicationFactory<Program> Factory(ReferenceCheck reference)
        => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ILegalEntityReferenceValidator>();
                services.RemoveAll<IPolicyMutationStore>();
                services.AddSingleton<ILegalEntityReferenceValidator>(new ReferenceValidator(reference));
                services.AddScoped<IPolicyMutationStore, SuccessfulStore>();
            }));

    private static HttpRequestMessage Request(string operation, string? token, bool company = true)
    {
        var policy = Guid.NewGuid();
        var revision = Guid.NewGuid();
        var path = operation switch
        {
            "create" => Route,
            "revise" => $"{Route}/{policy:D}/revisions",
            _ => $"{Route}/{policy:D}/revisions/{revision:D}/{operation}"
        };
        var payload = operation switch
        {
            "create" => JsonSerializer.Serialize(new { skuId = Guid.NewGuid(), warehouseId = Guid.NewGuid(), coverageDays = 5 }),
            "revise" => JsonSerializer.Serialize(new { sourceRevisionId = revision, coverageDays = 6, expectedVersion = 1, reason = "New coverage" }),
            _ => JsonSerializer.Serialize(new { expectedVersion = 1, reason = "Review decision" })
        };
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (company) request.Headers.Add("X-Legal-Entity-Id", Guid.NewGuid().ToString("D"));
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        return request;
    }

    private static string Token(string permission)
    {
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)), SecurityAlgorithms.HmacSha256);
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: "Diten.AuthService", audience: "DitenERP",
            claims:
            [
                new Claim("sub", Guid.NewGuid().ToString("D")),
                new Claim("tenant_id", Guid.NewGuid().ToString("D")),
                new Claim("permission", permission)
            ],
            notBefore: DateTime.UtcNow.AddMinutes(-1), expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials));
    }

    private sealed class ReferenceValidator(ReferenceCheck result) : ILegalEntityReferenceValidator
    {
        public Task<ReferenceCheck> ValidateAsync(Guid legalEntityId, CancellationToken cancellationToken)
            => Task.FromResult(result);
    }

    private sealed class SuccessfulStore : IPolicyMutationStore
    {
        public Task<PolicyMutationResult> ExecuteAsync(PolicyMutationCommand command, ActorSeriesScope scope,
            CancellationToken cancellationToken)
            => Task.FromResult(new PolicyMutationResult(PolicyMutationStatus.Created,
                new PolicyMutationReceipt(Guid.NewGuid(), Guid.NewGuid(), "Draft", 1)));
    }
}
