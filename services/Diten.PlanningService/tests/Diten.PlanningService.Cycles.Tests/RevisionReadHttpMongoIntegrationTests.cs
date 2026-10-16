using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.PlanningService.Api.Features.DemandPlanning;
using Diten.PlanningService.Application.Features.DemandPlanning;
using Diten.PlanningService.Application.Features.DemandPlanning.ManualDrafts;
using Diten.PlanningService.Domain.Features.DemandPlanning;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using Xunit;

namespace Diten.PlanningService.Cycles.Tests;

public sealed partial class PublishedRevisionMongoIntegrationTests
{
    private sealed class ReadHttpHost : IAsyncDisposable
    {
        private readonly WebApplication _app;
        private readonly string _secret;
        public HttpClient Client { get; }

        private ReadHttpHost(WebApplication app, string secret, HttpClient client)
        {
            _app = app;
            _secret = secret;
            Client = client;
        }

        public static async Task<ReadHttpHost> StartAsync(DemandRevisionDraft draft,
            IInternalRevisionStatusAuthority? statusAuthority = null,
            IInternalSnapshotReadAuthority? contentAuthority = null,
            IInternalInvalidatedHistoryAuthority? auditAuthority = null,
            IManualDraftAuthority? assignmentAuthority = null,
            IInternalPublishAuthority? publishAuthority = null,
            IInternalInvalidationAuthority? invalidationAuthority = null,
            bool allowPublishInTest = false,
            IInternalCurrentPublishedAuthority? currentAuthority = null)
        {
            var uri = Environment.GetEnvironmentVariable("MOD0188_TEST_MONGO_URI")
                ?? throw new InvalidOperationException("Explicit test Mongo URI is required.");
            var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = "Development"
            });
            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(options =>
                options.Listen(IPAddress.Loopback, 0));
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:Secret"] = secret,
                ["JwtSettings:Issuer"] = "mod0188-http-test-issuer",
                ["JwtSettings:Audience"] = "mod0188-http-test-audience",
                ["Mongo:ConnectionString"] = uri,
                ["Mongo:SupplyChainDatabaseName"] = "mod0188_tests",
                ["DemandPlanning:SnapshotCursorSigningKey"] =
                    Convert.ToHexString(RandomNumberGenerator.GetBytes(32))
            });
            builder.Services.AddControllers().AddApplicationPart(
                typeof(RevisionReadController).Assembly);
            builder.Services.AddDemandPlanningFeature(builder.Configuration);
            if (allowPublishInTest)
                builder.Services.PostConfigure<AuthorizationOptions>(options =>
                    options.AddPolicy("demand.plans.publish", policy => policy
                        .RequireAuthenticatedUser()
                        .RequireClaim("permission", "demand.plans.publish")));
            if (assignmentAuthority is not null)
                builder.Services.Replace(ServiceDescriptor.Singleton(assignmentAuthority));
            if (publishAuthority is not null)
                builder.Services.Replace(ServiceDescriptor.Singleton(publishAuthority));
            if (invalidationAuthority is not null)
                builder.Services.Replace(ServiceDescriptor.Singleton(invalidationAuthority));
            if (currentAuthority is not null)
                builder.Services.Replace(ServiceDescriptor.Singleton(currentAuthority));
            builder.Services.Replace(ServiceDescriptor.Singleton<IInternalRevisionStatusAuthority>(
                statusAuthority ?? new StatusAuthority(draft)));
            builder.Services.Replace(ServiceDescriptor.Singleton<IInternalSnapshotReadAuthority>(
                contentAuthority ?? new ReadAuthority([draft])));
            builder.Services.Replace(ServiceDescriptor.Singleton<IInternalInvalidatedHistoryAuthority>(
                auditAuthority ?? new InvalidationAuthority(draft)));
            var app = builder.Build();
            app.UseAuthentication();
            app.UseMiddleware<DemandTenantMiddleware>();
            app.UseAuthorization();
            app.MapControllers();
            await app.StartAsync();
            var address = app.Services.GetRequiredService<IServer>().Features
                .Get<IServerAddressesFeature>()?.Addresses.Single()
                ?? throw new InvalidOperationException("Test host did not bind loopback.");
            var client = new HttpClient(new HttpClientHandler { UseProxy = false })
            {
                BaseAddress = new Uri(address)
            };
            return new ReadHttpHost(app, secret, client);
        }

        public HttpRequestMessage Request(string path, Guid tenantId,
            Guid legalEntityId, Guid actorId,
            string[]? permissions = null, DateTime? expires = null,
            string? issuer = null, string? audience = null,
            string? signingSecret = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Add("X-Tenant-Id", tenantId.ToString("D"));
            request.Headers.Add("X-Legal-Entity-Id", legalEntityId.ToString("D"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer",
                Token(tenantId, actorId, permissions ?? ["demand.plans.consume"],
                    expires, issuer, audience, signingSecret));
            return request;
        }

        private string Token(Guid tenantId, Guid actorId,
            IReadOnlyList<string> permissions, DateTime? expires,
            string? issuer, string? audience, string? signingSecret)
        {
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, actorId.ToString("D")),
                new("tenant_id", tenantId.ToString("D"))
            };
            claims.AddRange(permissions.Select(permission =>
                new Claim("permission", permission)));
            var token = new JwtSecurityToken(
                issuer ?? "mod0188-http-test-issuer",
                audience ?? "mod0188-http-test-audience", claims,
                notBefore: DateTime.UtcNow.AddHours(-2),
                expires: expires ?? DateTime.UtcNow.AddMinutes(10),
                signingCredentials: new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                        signingSecret ?? _secret)), SecurityAlgorithms.HmacSha256));
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }

    private static async Task<JsonElement> SuccessData(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("isSuccessful").GetBoolean());
        return json.RootElement.GetProperty("data").Clone();
    }

    private sealed class RevocableReadAuthority(DemandRevisionDraft draft)
        : IInternalSnapshotReadAuthority
    {
        public bool ScopeAvailable { get; set; } = true;

        public async Task<SnapshotReadAuthorityEvidence> VerifyAsync(Guid tenantId,
            Guid legalEntityId, Guid revisionId, Guid actorId,
            CancellationToken cancellationToken)
        {
            var evidence = await new ReadAuthority([draft]).VerifyAsync(tenantId,
                legalEntityId, revisionId, actorId, cancellationToken);
            return evidence with
            {
                RevisionScopeVerified = ScopeAvailable &&
                    evidence.RevisionScopeVerified
            };
        }
    }

    [ManualDraftMongoFact]
    public async Task V2ReadHttp_JwtRoutingPermissionsTenantAndJson_AreEnforced()
    {
        var (draft, _, _, _) = await PublishedFixture(secondSeries: false);
        var actor = Guid.NewGuid();
        await using var host = await ReadHttpHost.StartAsync(draft);
        var path = $"/api/v2/demand/revisions/{draft.Id:D}";
        var client = host.Client;

        using (var response = await client.GetAsync(path + "/status"))
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using (var request = host.Request(path + "/status", draft.TenantId,
            draft.LegalEntityId, actor, expires: DateTime.UtcNow.AddMinutes(-2)))
        using (var response = await client.SendAsync(request))
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using (var request = host.Request(path + "/status", draft.TenantId,
            draft.LegalEntityId, actor, signingSecret: new string('X', 64)))
        using (var response = await client.SendAsync(request))
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using (var request = host.Request(path + "/status", draft.TenantId,
            draft.LegalEntityId, actor, issuer: "wrong-issuer"))
        using (var response = await client.SendAsync(request))
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using (var request = host.Request(path + "/status", draft.TenantId,
            draft.LegalEntityId, actor, audience: "wrong-audience"))
        using (var response = await client.SendAsync(request))
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using (var request = host.Request(path + "/status", draft.TenantId,
            draft.LegalEntityId, actor, permissions: []))
        using (var response = await client.SendAsync(request))
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using (var request = host.Request(path + "/status", Guid.NewGuid(),
            draft.LegalEntityId, actor))
        using (var response = await client.SendAsync(request))
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using (var request = host.Request(path + "/status", draft.TenantId,
            draft.LegalEntityId, actor))
        {
            request.Headers.Remove("X-Tenant-Id");
            request.Headers.Add("X-Tenant-Id", Guid.NewGuid().ToString("D"));
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        using (var request = host.Request(path + "/status", draft.TenantId,
            Guid.NewGuid(), actor))
        using (var response = await client.SendAsync(request))
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using (var request = host.Request(path + "/status", draft.TenantId,
            draft.LegalEntityId, actor))
        using (var response = await client.SendAsync(request))
        {
            var data = await SuccessData(response);
            Assert.Equal("Published", data.GetProperty("state").GetString());
            Assert.Equal(draft.TenantId.ToString("D"),
                data.GetProperty("tenantId").GetString());
        }
        using (var request = host.Request(path + "/manifest", draft.TenantId,
            draft.LegalEntityId, actor))
        using (var response = await client.SendAsync(request))
        {
            var data = await SuccessData(response);
            Assert.Equal("Published", data.GetProperty("state").GetString());
            Assert.Equal(52, data.GetProperty("calendar").GetProperty("weeks").GetArrayLength());
        }
        using (var request = host.Request(path + "/rows?pageSize=7", draft.TenantId,
            draft.LegalEntityId, actor))
        using (var response = await client.SendAsync(request))
        {
            var data = await SuccessData(response);
            Assert.Equal(7, data.GetProperty("returnedRowCount").GetInt32());
            Assert.False(string.IsNullOrWhiteSpace(
                data.GetProperty("nextCursor").GetString()));
        }
    }

    [ManualDraftMongoFact]
    public async Task V2ReadHttp_ScopeOutageAndAuditSeparation_FailClosed()
    {
        var (draft, drafts, published, context) =
            await PublishedFixture(secondSeries: false);
        var actor = Guid.NewGuid();
        var path = $"/api/v2/demand/revisions/{draft.Id:D}";
        await using (var hidden = await ReadHttpHost.StartAsync(draft,
            new StatusAuthority(draft, inScope: false),
            new ReadAuthority([draft], inScope: false),
            new InvalidationAuthority(draft, scopeVerified: false)))
        using (var request = hidden.Request(path + "/status", draft.TenantId,
            draft.LegalEntityId, actor))
        using (var response = await hidden.Client.SendAsync(request))
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await using (var outage = await ReadHttpHost.StartAsync(draft,
            new StatusAuthority(draft, sourceAvailable: false),
            new ReadAuthority([draft], sourceAvailable: false),
            new InvalidationAuthority(draft, sourceAvailable: false)))
        using (var request = outage.Request(path + "/manifest", draft.TenantId,
            draft.LegalEntityId, actor))
        using (var response = await outage.Client.SendAsync(request))
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        var replacement = Draft(draft.TenantId, draft.LegalEntityId,
            Guid.NewGuid(), FirstWeek);
        await Approve(drafts, replacement, replacement.CreatedBy, Guid.NewGuid());
        Assert.Equal(PublishOutcome.Published,
            (await Publish(published, replacement, Guid.NewGuid())).Outcome);
        await using (var supersededHost = await ReadHttpHost.StartAsync(draft))
        {
            using (var request = supersededHost.Request(path + "/status",
                draft.TenantId, draft.LegalEntityId, actor))
            using (var response = await supersededHost.Client.SendAsync(request))
                Assert.Equal("Superseded", (await SuccessData(response))
                    .GetProperty("state").GetString());
            using (var request = supersededHost.Request(path + "/rows",
                draft.TenantId, draft.LegalEntityId, actor))
            using (var response = await supersededHost.Client.SendAsync(request))
                Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }
        Assert.Equal(InvalidationOutcome.Invalidated,
            (await Invalidate(Invalidator(draft, drafts, published, context),
                draft, stateVersion: 4)).Outcome);
        await using var host = await ReadHttpHost.StartAsync(draft);
        using (var request = host.Request(path + "/status", draft.TenantId,
            draft.LegalEntityId, actor))
        using (var response = await host.Client.SendAsync(request))
            Assert.Equal("Invalidated", (await SuccessData(response))
                .GetProperty("state").GetString());
        using (var request = host.Request(path + "/rows", draft.TenantId,
            draft.LegalEntityId, actor))
        using (var response = await host.Client.SendAsync(request))
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using (var request = host.Request(path + "/audit-snapshot", draft.TenantId,
            draft.LegalEntityId, actor))
        using (var response = await host.Client.SendAsync(request))
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using (var request = host.Request(path + "/audit-rows?pageSize=4",
            draft.TenantId, draft.LegalEntityId, actor,
            ["demand.audit.read"]))
        using (var response = await host.Client.SendAsync(request))
        {
            var data = await SuccessData(response);
            Assert.Contains("kullanılamaz", data.GetProperty("warning").GetString());
            Assert.Equal(4, data.GetProperty("page").GetProperty("returnedRowCount")
                .GetInt32());
        }
    }

    [ManualDraftMongoFact]
    public async Task V2ReadHttp_CursorScopeLossAndCorruptSnapshot_FailClosed()
    {
        var (draft, _, _, context) = await PublishedFixture(secondSeries: false);
        var actor = Guid.NewGuid();
        var authority = new RevocableReadAuthority(draft);
        await using var host = await ReadHttpHost.StartAsync(draft,
            contentAuthority: authority);
        var path = $"/api/v2/demand/revisions/{draft.Id:D}";
        using var firstRequest = host.Request(path + "/rows?pageSize=7",
            draft.TenantId, draft.LegalEntityId, actor);
        using var firstResponse = await host.Client.SendAsync(firstRequest);
        var first = await SuccessData(firstResponse);
        var cursor = first.GetProperty("nextCursor").GetString()!;
        using (var request = host.Request(path + "/rows?pageSize=7&cursor=" +
            Uri.EscapeDataString(cursor), draft.TenantId, draft.LegalEntityId, actor))
        using (var response = await host.Client.SendAsync(request))
            Assert.Equal(8, (await SuccessData(response)).GetProperty("items")
                .EnumerateArray().First().GetProperty("weekNumber").GetInt32());
        using (var request = host.Request(path + "/rows?pageSize=7&cursor=altered",
            draft.TenantId, draft.LegalEntityId, actor))
        using (var response = await host.Client.SendAsync(request))
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        authority.ScopeAvailable = false;
        using (var request = host.Request(path + "/rows?pageSize=7&cursor=" +
            Uri.EscapeDataString(cursor), draft.TenantId, draft.LegalEntityId, actor))
        using (var response = await host.Client.SendAsync(request))
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        authority.ScopeAvailable = true;

        var part = await context.PublishedRevisionParts.Find(x =>
            x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId &&
            x.RevisionId == draft.Id).SingleAsync();
        part.Rows.RemoveAt(0);
        await context.PublishedRevisionParts.ReplaceOneAsync(x =>
            x.Id == part.Id && x.TenantId == draft.TenantId &&
            x.LegalEntityId == draft.LegalEntityId, part);
        using (var request = host.Request(path + "/manifest", draft.TenantId,
            draft.LegalEntityId, actor))
        using (var response = await host.Client.SendAsync(request))
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        using (var request = host.Request(path + "/status", draft.TenantId,
            draft.LegalEntityId, actor))
        using (var response = await host.Client.SendAsync(request))
            Assert.Equal("Published", (await SuccessData(response))
                .GetProperty("state").GetString());
    }
}
