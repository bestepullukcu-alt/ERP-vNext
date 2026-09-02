using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Diten.ManagementGovernanceService.Api.Controllers;
using Diten.ManagementGovernanceService.Application;
using Diten.ManagementGovernanceService.Application.Modules.ProcessModeling.Catalog;
using Diten.ManagementGovernanceService.Persistence.Modules.ProcessModeling.Catalog;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.ManagementGovernanceService.IntegrationTests.Modules.ProcessModeling.Catalog;

[Collection(CatalogMongoCollection.Name)]
public sealed class CatalogEphemeralHttpTests(DisposableCatalogMongo mongo)
{
    private const string BasePath = "/internal/local-test/v1/process-modeling";
    private const string CreatePermission = "management-governance.process-modeling.architectures.create";
    private const string ReadPermission = "management-governance.process-modeling.architectures.read";

    [Fact]
    public async Task Ephemeral_HTTP_host_exposes_real_CQRS_Mongo_create_replay_tree_and_CAS_conflict()
    {
        await using var host = await CatalogHttpHost.StartAsync(mongo.Context);
        var tenant = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var id = Guid.NewGuid();

        using var create = Request(HttpMethod.Post, $"{BasePath}/catalog/architectures", tenant, actor, CreatePermission, "http-replay");
        create.Content = JsonContent.Create(new { id, architectureCode = "ORDER-TO-CASH", name = "Order to Cash", description = (string?)null, sortOrder = 10 });
        using var created = await host.Client.SendAsync(create);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var replay = Request(HttpMethod.Post, $"{BasePath}/catalog/architectures", tenant, actor, CreatePermission, "http-replay");
        replay.Content = JsonContent.Create(new { id, architectureCode = "ORDER-TO-CASH", name = "Order to Cash", description = (string?)null, sortOrder = 10 });
        Assert.Equal(HttpStatusCode.Created, (await host.Client.SendAsync(replay)).StatusCode);

        using var tree = Request(HttpMethod.Get, $"{BasePath}/catalog/tree", tenant, actor, ReadPermission);
        using var treeResponse = await host.Client.SendAsync(tree);
        var treeBody = await treeResponse.Content.ReadAsStringAsync();
        Assert.True(treeResponse.StatusCode == HttpStatusCode.OK, treeBody);
        Assert.Contains(id.ToString(), treeBody, StringComparison.OrdinalIgnoreCase);

        using var stale = Request(HttpMethod.Put, $"{BasePath}/catalog/architectures/{id:D}", tenant, actor,
            "management-governance.process-modeling.architectures.update", "http-stale");
        stale.Content = JsonContent.Create(new { name = "Changed", description = (string?)null, sortOrder = 11, expectedVersion = 99 });
        Assert.Equal(HttpStatusCode.Conflict, (await host.Client.SendAsync(stale)).StatusCode);
    }

    [Fact]
    public async Task All_four_catalog_levels_persist_trimmed_NFC_and_normalized_codes()
    {
        await using var host = await CatalogHttpHost.StartAsync(mongo.Context);
        var tenant = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var architectureId = Guid.NewGuid();
        var domainId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var definitionId = Guid.NewGuid();
        var decomposed = "Cafe\u0301";

        Assert.Equal(HttpStatusCode.Created, await Post(host, "catalog/architectures", tenant, actor, CreatePermission, "norm-architecture",
            new { id = architectureId, architectureCode = " order architecture ", name = $"  {decomposed}  ", description = "  architecture description  ", sortOrder = 1 }));
        Assert.Equal(HttpStatusCode.Created, await Post(host, "catalog/domains", tenant, actor, CreatePermission, "norm-domain",
            new { id = domainId, processArchitectureId = architectureId, domainCode = "sales_ops", name = "  Sales  ", description = "  domain description  ", sortOrder = 2 }));
        Assert.Equal(HttpStatusCode.Created, await Post(host, "catalog/families", tenant, actor, CreatePermission, "norm-family",
            new { id = familyId, processDomainId = domainId, familyCode = "lead management", name = "  Leads  ", description = "  family description  ", sortOrder = 3 }));
        Assert.Equal(HttpStatusCode.Created, await Post(host, "catalog/definitions", tenant, actor,
            "management-governance.process-modeling.definitions.create", "norm-definition",
            new { id = definitionId, processFamilyId = familyId, processCode = "qualify_lead", name = "  Qualification  ", purpose = "  purpose  ", description = "  definition description  " }));

        using var tree = Request(HttpMethod.Get, $"{BasePath}/catalog/tree", tenant, actor, ReadPermission);
        var body = await (await host.Client.SendAsync(tree)).Content.ReadAsStringAsync();
        Assert.Contains("ORDER-ARCHITECTURE", body, StringComparison.Ordinal);
        Assert.Contains("SALES-OPS", body, StringComparison.Ordinal);
        Assert.Contains("LEAD-MANAGEMENT", body, StringComparison.Ordinal);
        Assert.Contains("QUALIFY-LEAD", body, StringComparison.Ordinal);
        Assert.Contains("Café", body, StringComparison.Ordinal);
        Assert.DoesNotContain("  architecture description  ", body, StringComparison.Ordinal);
        Assert.Contains("architecture description", body, StringComparison.Ordinal);
        Assert.Contains("\"purpose\":\"purpose\"", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HTTP_rejects_all_code_shapes_and_text_bounds_with_400_across_all_four_levels()
    {
        await using var host = await CatalogHttpHost.StartAsync(mongo.Context);
        var tenant = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var parent = Guid.NewGuid();
        var cases = new (string Path, string Permission, object Body)[]
        {
            ("catalog/architectures", CreatePermission, new { id = Guid.NewGuid(), architectureCode = "", name = "Name", description = (string?)null, sortOrder = 0 }),
            ("catalog/architectures", CreatePermission, new { id = Guid.NewGuid(), architectureCode = new string('A', 101), name = "Name", description = (string?)null, sortOrder = 0 }),
            ("catalog/architectures", CreatePermission, new { id = Guid.NewGuid(), architectureCode = "BAD@CODE", name = "Name", description = (string?)null, sortOrder = 0 }),
            ("catalog/domains", CreatePermission, new { id = Guid.NewGuid(), processArchitectureId = parent, domainCode = "-DOMAIN", name = "Name", description = (string?)null, sortOrder = 0 }),
            ("catalog/domains", CreatePermission, new { id = Guid.NewGuid(), processArchitectureId = parent, domainCode = "DOMAIN-", name = "Name", description = new string('D', 2001), sortOrder = 0 }),
            ("catalog/families", CreatePermission, new { id = Guid.NewGuid(), processDomainId = parent, familyCode = "FAMILY--CODE", name = new string('N', 201), description = (string?)null, sortOrder = 0 }),
            ("catalog/definitions", "management-governance.process-modeling.definitions.create", new { id = Guid.NewGuid(), processFamilyId = parent, processCode = "PROCESS", name = "Name", purpose = new string('P', 2001), description = (string?)null }),
            ("catalog/definitions", "management-governance.process-modeling.definitions.create", new { id = Guid.NewGuid(), processFamilyId = parent, processCode = "PROCESS", name = "Name", purpose = (string?)null, description = new string('D', 4001) })
        };

        for (var index = 0; index < cases.Length; index++)
        {
            var item = cases[index];
            Assert.Equal(HttpStatusCode.BadRequest, await Post(host, item.Path, tenant, actor, item.Permission, $"invalid-{index}", item.Body));
        }
    }

    [Fact]
    public async Task HTTP_security_and_visibility_matrix_is_exact_400_401_403_404()
    {
        await using var host = await CatalogHttpHost.StartAsync(mongo.Context);
        var tenant = Guid.NewGuid();
        var actor = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync($"{BasePath}/catalog/tree")).StatusCode);

        using var conflict = Request(HttpMethod.Get, $"{BasePath}/catalog/tree", tenant, actor, ReadPermission);
        conflict.Headers.Remove("X-Tenant-Id");
        conflict.Headers.Add("X-Tenant-Id", Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.SendAsync(conflict)).StatusCode);

        using var denied = Request(HttpMethod.Get, $"{BasePath}/catalog/tree", tenant, actor, "wrong.permission");
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.SendAsync(denied)).StatusCode);

        using var missing = Request(HttpMethod.Get, $"{BasePath}/catalog/definitions/{Guid.NewGuid():D}", tenant, actor,
            "management-governance.process-modeling.definitions.read");
        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.SendAsync(missing)).StatusCode);
    }

    [Fact]
    public async Task All_fourteen_routes_require_authenticated_fixture_credential()
    {
        await using var host = await CatalogHttpHost.StartAsync(mongo.Context);
        var id = Guid.NewGuid();
        var routes = new (HttpMethod Method, string Path)[]
        {
            (HttpMethod.Get, "catalog/tree"), (HttpMethod.Get, $"catalog/definitions/{id:D}"),
            (HttpMethod.Post, "catalog/architectures"), (HttpMethod.Put, $"catalog/architectures/{id:D}"), (HttpMethod.Post, $"catalog/architectures/{id:D}/archive"),
            (HttpMethod.Post, "catalog/domains"), (HttpMethod.Put, $"catalog/domains/{id:D}"), (HttpMethod.Post, $"catalog/domains/{id:D}/archive"),
            (HttpMethod.Post, "catalog/families"), (HttpMethod.Put, $"catalog/families/{id:D}"), (HttpMethod.Post, $"catalog/families/{id:D}/archive"),
            (HttpMethod.Post, "catalog/definitions"), (HttpMethod.Put, $"catalog/definitions/{id:D}"), (HttpMethod.Post, $"catalog/definitions/{id:D}/archive")
        };
        Assert.Equal(14, routes.Length);

        foreach (var route in routes)
        {
            using var request = new HttpRequestMessage(route.Method, $"{BasePath}/{route.Path}");
            if (route.Method != HttpMethod.Get) request.Content = JsonContent.Create(new { });
            Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.SendAsync(request)).StatusCode);
        }
    }

    private static HttpRequestMessage Request(HttpMethod method, string path, Guid tenant, Guid actor, string permission, string? key = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-Test-Tenant", tenant.ToString());
        request.Headers.Add("X-Test-Actor", actor.ToString());
        request.Headers.Add("X-Test-Permission", permission);
        request.Headers.Add("X-Tenant-Id", tenant.ToString());
        if (key is not null) request.Headers.Add("Idempotency-Key", key);
        return request;
    }

    private static async Task<HttpStatusCode> Post(CatalogHttpHost host, string path, Guid tenant, Guid actor, string permission, string key, object body)
    {
        using var request = Request(HttpMethod.Post, $"{BasePath}/{path}", tenant, actor, permission, key);
        request.Content = JsonContent.Create(body);
        using var response = await host.Client.SendAsync(request);
        return response.StatusCode;
    }

    private sealed class CatalogHttpHost(WebApplication app, HttpClient client) : IAsyncDisposable
    {
        public HttpClient Client { get; } = client;

        public static async Task<CatalogHttpHost> StartAsync(CatalogMongoContext context)
        {
            var builder = WebApplication.CreateSlimBuilder();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services.AddControllers().AddApplicationPart(typeof(ProcessModelingLocalTestController).Assembly);
            builder.Services.AddDwsApplication();
            builder.Services.AddSingleton<ICatalogStore>(new MongoCatalogStore(new CatalogMongoStore(context)));
            builder.Services.AddAuthentication("catalog-fixture")
                .AddScheme<AuthenticationSchemeOptions, HeaderFixtureAuthenticationHandler>("catalog-fixture", _ => { });
            builder.Services.AddAuthorization();
            var app = builder.Build();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();
            await app.StartAsync();
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            return new CatalogHttpHost(app, new HttpClient { BaseAddress = new Uri(address) });
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }

    private sealed class HeaderFixtureAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Guid.TryParse(Request.Headers["X-Test-Tenant"], out var tenant)
                || !Guid.TryParse(Request.Headers["X-Test-Actor"], out var actor))
                return Task.FromResult(AuthenticateResult.NoResult());

            var claims = new List<Claim> { new("tenant_id", tenant.ToString()), new(ClaimTypes.NameIdentifier, actor.ToString()) };
            claims.AddRange(Request.Headers["X-Test-Permission"].Select(permission => new Claim("permission", permission!)));
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }
}
