using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Diten.ManufacturingService.Application.Common;
using Diten.ManufacturingService.Infrastructure.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using Xunit;

namespace Diten.ManufacturingService.Tests.Api;

/// <summary>
/// Hosts the REAL Program against an isolated Mongo replica set named by <c>MANUFACTURING_TEST_MONGO</c>. One fixed
/// database (MongoTestDatabaseGuard: no database per run); every test isolates itself with a fresh tenant +
/// legal entity, which is also what the rule under test is about.
/// </summary>
public sealed class BomApiFactory : WebApplicationFactory<Program>
{
    public const string EnvironmentVariable = "MANUFACTURING_TEST_MONGO";
    public const string Database = "diten_manufacturing_tests";
    private const string Issuer = "manufacturing-tests";
    private static readonly string Secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    public static string? Connection => Environment.GetEnvironmentVariable(EnvironmentVariable);

    public UnknownItemsValidator Products { get; } = new();

    private static Dictionary<string, string?> Settings => new()
    {
        ["Mongo:ConnectionString"] = Connection,
        ["Mongo:DatabaseName"] = Database,
        ["JwtSettings:Secret"] = Secret,
        ["JwtSettings:Issuer"] = Issuer,
        ["JwtSettings:Audience"] = Issuer,
        ["ProductMaster:Mode"] = "Permissive",
        ["PlatformRegistration:InternalApiKey"] = ""
    };

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(c => c.AddInMemoryCollection(Settings));
        return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(Settings));
        builder.ConfigureServices(s => s.Replace(ServiceDescriptor.Singleton<IProductReferenceValidator>(Products)));
    }

    public IMongoDatabase Db() => Services.GetRequiredService<IMongoClient>().GetDatabase(Database);

    public static string Token(Guid tenant, Guid legalEntity, Guid actor, IEnumerable<string>? permissions = null)
    {
        var claims = new List<Claim>
        {
            new("tenant_id", tenant.ToString()),
            new("legal_entity_id", legalEntity.ToString()),
            new("sub", actor.ToString()),
            new("name", "BOM Tester")
        };
        claims.AddRange((permissions ?? BomPermissions.All).Select(p => new Claim("permission", p)));
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(Issuer, Issuer, claims, expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)), SecurityAlgorithms.HmacSha256)));
    }
}

/// <summary>Fake PRODUCT-MASTER: every id is known unless added to <see cref="Unknown"/>.</summary>
public sealed class UnknownItemsValidator : IProductReferenceValidator
{
    public HashSet<Guid> Unknown { get; } = [];

    public Task<IReadOnlyList<Guid>> GetUnknownItemIdsAsync(IReadOnlyCollection<Guid> itemIds, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Guid>>(itemIds.Where(Unknown.Contains).ToList());
}

/// <summary>One tenant + legal entity + actor, and a client that speaks for them.</summary>
public sealed class BomCaller(BomApiFactory factory, Guid? tenant = null, Guid? legalEntity = null, IEnumerable<string>? permissions = null)
{
    public Guid Tenant { get; } = tenant ?? Guid.NewGuid();
    public Guid LegalEntity { get; } = legalEntity ?? Guid.NewGuid();
    public Guid Actor { get; } = Guid.NewGuid();
    private readonly HttpClient _client = factory.CreateClient();
    private readonly IEnumerable<string>? _permissions = permissions;

    public async Task<(int Status, JsonNode? Body, HttpResponseMessage Raw)> Send(HttpMethod method, string path, object? body = null, string? correlation = null, bool authenticated = true)
    {
        using var request = new HttpRequestMessage(method, path);
        if (authenticated)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", BomApiFactory.Token(Tenant, LegalEntity, Actor, _permissions));
        }

        request.Headers.TryAddWithoutValidation("X-Tenant-Id", Tenant.ToString());
        request.Headers.TryAddWithoutValidation("X-Legal-Entity-Id", LegalEntity.ToString());
        if (correlation is not null)
        {
            request.Headers.TryAddWithoutValidation("X-Correlation-Id", correlation);
        }

        if (body is not null)
        {
            request.Content = body is string raw ? new StringContent(raw, Encoding.UTF8, "application/json") : JsonContent.Create(body);
        }

        var response = await _client.SendAsync(request);
        var json = response.Content.Headers.ContentType?.MediaType == "application/json";
        var text = json ? await response.Content.ReadAsStringAsync() : string.Empty;
        return ((int)response.StatusCode, string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text), response);
    }

    public static object Draft(Guid itemId, params (Guid Component, string Quantity, string Uom, int Position)[] lines) => new
    {
        itemId,
        description = "Formula",
        components = lines.Select(l => new { componentItemId = l.Component, quantity = l.Quantity, uomId = l.Uom, position = l.Position }).ToArray(),
        routing = new { steps = new[] { new { stepNo = 10, operation = "Karıştırma", workCenter = "WC-01" } } }
    };

    public async Task<JsonNode> CreateDraft(Guid itemId, params (Guid, string, string, int)[] lines)
    {
        var (status, body, _) = await Send(HttpMethod.Post, "/api/bom/versions", Draft(itemId, lines));
        Assert.True(status == 201, $"create draft answered {status}: {body}");
        return body!;
    }

    public async Task<JsonNode> Release(JsonNode draft, string changeControlRef = "CC-2026-001")
    {
        var (status, body, _) = await Send(HttpMethod.Post, $"/api/bom/version/{draft["bomVersionId"]}/release",
            new { changeControlRef, rowVersion = (int)draft["rowVersion"]! });
        Assert.True(status == 200, $"release answered {status}: {body}");
        return body!;
    }
}
