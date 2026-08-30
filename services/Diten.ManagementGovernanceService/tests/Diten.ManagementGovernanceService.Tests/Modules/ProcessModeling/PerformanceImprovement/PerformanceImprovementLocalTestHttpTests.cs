using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Diten.ManagementGovernanceService.Api.Controllers;
using Diten.ManagementGovernanceService.Api.LocalTestSecurity;
using Diten.ManagementGovernanceService.Application;
using Diten.ManagementGovernanceService.Application.Modules.ProcessModeling.PerformanceImprovement;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Diten.ManagementGovernanceService.Tests.Modules.ProcessModeling.PerformanceImprovement;

public sealed class PerformanceImprovementLocalTestHttpTests
{
    private static readonly Guid Tenant = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid Actor = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid Opportunity = Guid.Parse("33333333-3333-4333-8333-333333333333");

    [Fact]
    public async Task Read_boundary_is_401_400_403_then_tenant_scoped_404_without_disclosure()
    {
        var queries = new QueryStore();
        await using (var anonymous = await Host.StartAsync(null, queries, new MutationStore()))
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.Client.GetAsync(Path())).StatusCode);

        await using (var authorized = await Host.StartAsync(Identity(PerformanceImprovementPermissions.ImprovementRead), queries, new MutationStore()))
        {
            using var mismatch = Get(Path(), Guid.NewGuid());
            Assert.Equal(HttpStatusCode.BadRequest, (await authorized.Client.SendAsync(mismatch)).StatusCode);
            Assert.Equal(0, queries.Calls);
        }

        await using (var denied = await Host.StartAsync(Identity(PerformanceImprovementPermissions.ImprovementCreate), queries, new MutationStore()))
        {
            using var request = Get(Path(), Tenant);
            Assert.Equal(HttpStatusCode.Forbidden, (await denied.Client.SendAsync(request)).StatusCode);
            Assert.Equal(0, queries.Calls);
        }

        await using (var allowed = await Host.StartAsync(Identity(PerformanceImprovementPermissions.ImprovementRead), queries, new MutationStore()))
        {
            using var request = Get(Path(), Tenant);
            Assert.Equal(HttpStatusCode.NotFound, (await allowed.Client.SendAsync(request)).StatusCode);
            Assert.Equal(1, queries.Calls);
            Assert.Equal(Tenant, queries.LastTenant);
        }
    }

    [Theory]
    [InlineData(409, "performance_improvement_conflict")]
    [InlineData(503, "performance_improvement_unavailable")]
    public async Task Mutation_maps_stale_and_unavailable_without_extra_reads(int status, string code)
    {
        var queries = new QueryStore();
        var mutations = new MutationStore(new(false, status, code, Opportunity));
        await using var host = await Host.StartAsync(Identity(PerformanceImprovementPermissions.ImprovementUpdate), queries, mutations);
        using var request = new HttpRequestMessage(HttpMethod.Put, Path())
        {
            Content = new StringContent("""{"title":"title","problemStatement":"problem","expectedOutcome":"expected","priorityRank":1,"expectedVersion":0}""", Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(LocalTestAuthenticationHandler.SchemeName);
        request.Headers.Add("X-Tenant-Id", Tenant.ToString("D"));
        request.Headers.Add("Idempotency-Key", "http-test");
        Assert.Equal(status, (int)(await host.Client.SendAsync(request)).StatusCode);
        Assert.Equal(1, mutations.Calls);
        Assert.Equal(0, queries.Calls);
    }

    [Fact]
    public async Task Request_binding_includes_exact_route_identity_and_pack_command_name()
    {
        var mutations = new MutationStore();
        await using var host = await Host.StartAsync(
            Identity(PerformanceImprovementPermissions.ImprovementUpdate), new QueryStore(), mutations);
        var body = """{"title":"title","problemStatement":"problem","expectedOutcome":"expected","priorityRank":1,"expectedVersion":0}""";

        foreach (var id in new[] { Opportunity, Guid.Parse("44444444-4444-4444-8444-444444444444") })
        {
            using var request = new HttpRequestMessage(HttpMethod.Put, $"/internal/local-test/v1/process-modeling/improvements/{id:D}")
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue(LocalTestAuthenticationHandler.SchemeName);
            request.Headers.Add("X-Tenant-Id", Tenant.ToString("D"));
            request.Headers.Add("Idempotency-Key", "same-key");
            Assert.Equal(HttpStatusCode.OK, (await host.Client.SendAsync(request)).StatusCode);
        }

        Assert.Equal(2, mutations.Requests.Count);
        Assert.NotEqual(mutations.Requests[0].CanonicalPayloadHash, mutations.Requests[1].CanonicalPayloadHash);
        var expected = Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest.ProcessModelingLocalRequestHash.Compute(
            "PUT", $"/internal/local-test/v1/process-modeling/improvements/{Opportunity:D}",
            "UpdateProcessImprovementOpportunityCommand", Tenant, Actor,
            PerformanceImprovementPermissions.ImprovementUpdate, Encoding.UTF8.GetBytes(body));
        Assert.Equal(expected, mutations.Requests[0].CanonicalPayloadHash);
    }

    private static string Path() => $"/internal/local-test/v1/process-modeling/improvements/{Opportunity:D}";
    private static HttpRequestMessage Get(string path, Guid tenant)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue(LocalTestAuthenticationHandler.SchemeName);
        request.Headers.Add("X-Tenant-Id", tenant.ToString("D"));
        return request;
    }
    private static LocalTestIdentity Identity(params string[] permissions) => new(Actor, Tenant, Guid.NewGuid(), permissions);

    private sealed class QueryStore : PerformanceImprovementContract.IQueryStore
    {
        public int Calls { get; private set; }
        public Guid LastTenant { get; private set; }
        public Task<IReadOnlyList<PerformanceImprovementContract.OpportunityDto>> ListAsync(Guid tenantId, CancellationToken cancellationToken = default)
        { Calls++; LastTenant = tenantId; return Task.FromResult<IReadOnlyList<PerformanceImprovementContract.OpportunityDto>>([]); }
        public Task<PerformanceImprovementContract.OpportunityDto?> GetAsync(Guid tenantId, Guid opportunityId, CancellationToken cancellationToken = default)
        { Calls++; LastTenant = tenantId; return Task.FromResult<PerformanceImprovementContract.OpportunityDto?>(null); }
    }

    private sealed class MutationStore(PerformanceImprovementContract.MutationResult? result = null) : PerformanceImprovementContract.IMutationStore
    {
        public int Calls { get; private set; }
        public List<PerformanceImprovementContract.MutationRequest> Requests { get; } = [];
        public Task<PerformanceImprovementContract.MutationResult> ExecuteAsync(PerformanceImprovementContract.MutationRequest request, CancellationToken cancellationToken = default)
        { Calls++; Requests.Add(request); return Task.FromResult(result ?? new(true, 200, "accepted", request.AggregateId)); }
    }
    private sealed class IdentitySource(LocalTestIdentity? identity) : ILocalTestIdentitySource { public LocalTestIdentity? Resolve() => identity; }

    private sealed class Host(WebApplication application, HttpClient client) : IAsyncDisposable
    {
        public HttpClient Client { get; } = client;
        public static async Task<Host> StartAsync(LocalTestIdentity? identity, QueryStore query, MutationStore mutation)
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.AddManagementGovernanceApplication();
            builder.Services.AddSingleton<PerformanceImprovementContract.IQueryStore>(query);
            builder.Services.AddSingleton<PerformanceImprovementContract.IMutationStore>(mutation);
            if (identity is not null) builder.Services.AddSingleton<ILocalTestIdentitySource>(new IdentitySource(identity));
            builder.Services.AddAuthentication(LocalTestAuthenticationHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, LocalTestAuthenticationHandler>(LocalTestAuthenticationHandler.SchemeName, _ => { });
            builder.Services.AddAuthorization();
            builder.Services.AddControllers().AddApplicationPart(typeof(PerformanceImprovementLocalTestController).Assembly);
            var app = builder.Build(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers(); await app.StartAsync();
            return new(app, app.GetTestClient());
        }
        public async ValueTask DisposeAsync() { Client.Dispose(); await application.StopAsync(); await application.DisposeAsync(); }
    }
}
