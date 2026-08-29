using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Diten.ManagementGovernanceService.Api;
using Diten.ManagementGovernanceService.Api.LocalTest;
using Diten.ManagementGovernanceService.Application.Features.Dws;
using Diten.ManagementGovernanceService.Application.Modules.Dws;
using Diten.ManagementGovernanceService.Domain.Modules.Dws;
using Diten.ManagementGovernanceService.Infrastructure.Modules.Dws;
using Diten.ManagementGovernanceService.Persistence.Modules.Dws;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.ManagementGovernanceService.IntegrationTests.Modules.Dws;

[Collection(DwsMongoCollection.Name)]
public sealed class DwsFunctionalLocalHttpTests(DisposableDwsMongo mongo)
{
    [Fact]
    public async Task Malformed_raw_JSON_is_exact_400_Response_with_zero_provider_or_persistence_effect_and_host_stays_healthy()
    {
        Assert.False(await IsOccupiedAsync(5017), "protected local-test HTTP port 5017 is already occupied");
        var database = "mod0354_http_raw_" + Guid.NewGuid().ToString("N");
        var uri = $"mongodb://127.0.0.1:{mongo.Port}/?replicaSet={mongo.ReplicaSetName}&directConnection=true&serverSelectionTimeoutMS=1000";
        var app = Program.BuildLocalTestApp(uri, database);
        await app.Services.GetRequiredService<DwsMongoIndexInitializer>().InitializeAsync();
        var tenant = Guid.NewGuid();
        app.Services.GetRequiredService<DwsLocalTestIdentityFixture>().Configure(
            new(true, tenant, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));
        // The MOD-0117 and FU16 fixtures intentionally remain unconfigured. A request that reaches either
        // provider returns 503, so an exact 400 proves rejection happened at the HTTP/model boundary.
        await app.StartAsync();
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5017") };
            var referenceId = Guid.NewGuid();
            var reference = $"\"externalContextReference\":{{\"contractName\":\"ppm.external-context-reference\",\"contractVersion\":\"1.0\",\"contextKind\":0,\"contextId\":\"{referenceId:D}\"}}";
            var cases = new (string Name, byte[] Body)[]
            {
                ("syntax", Encoding.UTF8.GetBytes("{")),
                ("duplicate", Encoding.UTF8.GetBytes($"{{{reference},\"name\":\"A\",\"name\":\"B\",\"description\":null}}")),
                ("case-duplicate", Encoding.UTF8.GetBytes($"{{{reference},\"name\":\"A\",\"Name\":\"B\",\"description\":null}}")),
                ("invalid-surrogate", Encoding.UTF8.GetBytes($"{{{reference},\"name\":\"\\uD800\",\"description\":null}}")),
                ("invalid-utf8", [.. Encoding.UTF8.GetBytes($"{{{reference},\"name\":\""), 0xff, .. Encoding.UTF8.GetBytes("\",\"description\":null}")])
            };

            foreach (var testCase in cases)
            {
                using var response = await SendRawCreateAsync(client, testCase.Body, "raw-" + testCase.Name);
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                using var envelope = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync());
                Assert.False(envelope.RootElement.GetProperty("isSuccessful").GetBoolean());
                Assert.Equal(400, envelope.RootElement.GetProperty("statusCode").GetInt32());
                Assert.Contains(DwsErrors.InvalidRequest,
                    envelope.RootElement.GetProperty("errors").EnumerateArray().Select(error => error.GetString()));
                Assert.Equal(0, await CountTenantDocumentsAsync(app.Services, tenant));
                Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
            }
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
            await mongo.Client.DropDatabaseAsync(database);
        }
    }

    [Fact]
    public async Task All_fifteen_functional_routes_require_local_test_authentication()
    {
        Assert.False(await IsOccupiedAsync(5017), "protected local-test HTTP port 5017 is already occupied");
        var database = "mod0354_http_auth_" + Guid.NewGuid().ToString("N");
        var uri = $"mongodb://127.0.0.1:{mongo.Port}/?replicaSet={mongo.ReplicaSetName}&directConnection=true&serverSelectionTimeoutMS=1000";
        var app = Program.BuildLocalTestApp(uri, database);
        await app.Services.GetRequiredService<DwsMongoIndexInitializer>().InitializeAsync();
        app.Services.GetRequiredService<DwsLocalTestIdentityFixture>().Configure(
            new(false, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));
        await app.StartAsync();
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5017") };
            var id = Guid.NewGuid();
            var node = Guid.NewGuid();
            var requests = new[]
            {
                new HttpRequestMessage(HttpMethod.Post, "/api/dws/structures"),
                new HttpRequestMessage(HttpMethod.Put, $"/api/dws/structures/{id:D}/metadata"),
                new HttpRequestMessage(HttpMethod.Post, $"/api/dws/structures/{id:D}/nodes"),
                new HttpRequestMessage(HttpMethod.Post, $"/api/dws/structures/{id:D}/nodes/{node:D}/move"),
                new HttpRequestMessage(HttpMethod.Post, $"/api/dws/structures/{id:D}/nodes/{node:D}/reorder"),
                new HttpRequestMessage(HttpMethod.Delete, $"/api/dws/structures/{id:D}/nodes/{node:D}"),
                new HttpRequestMessage(HttpMethod.Post, $"/api/dws/structures/{id:D}/dependencies"),
                new HttpRequestMessage(HttpMethod.Delete, $"/api/dws/structures/{id:D}/dependencies"),
                new HttpRequestMessage(HttpMethod.Post, $"/api/dws/structures/{id:D}/baselines"),
                new HttpRequestMessage(HttpMethod.Post, $"/api/dws/structures/{id:D}/revisions"),
                new HttpRequestMessage(HttpMethod.Get, $"/api/dws/structures/{id:D}"),
                new HttpRequestMessage(HttpMethod.Get, $"/api/dws/structures/{id:D}/tree"),
                new HttpRequestMessage(HttpMethod.Get, $"/api/dws/structures/{id:D}/validation"),
                new HttpRequestMessage(HttpMethod.Get, $"/api/dws/structures/{id:D}/revision-comparison?left=1&right=2"),
                new HttpRequestMessage(HttpMethod.Get, $"/api/dws/structures/{id:D}/baseline-comparison?left=1&right=2")
            };
            Assert.Equal(15, requests.Length);
            foreach (var request in requests)
            {
                if (request.Method != HttpMethod.Get) request.Content = JsonContent.Create(new { });
                Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(request)).StatusCode);
                request.Dispose();
            }
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
            await mongo.Client.DropDatabaseAsync(database);
        }
    }

    [Fact]
    public async Task Local_5017_exposes_exact_201_200_400_401_403_404_409_503_matrix()
    {
        Assert.False(await IsOccupiedAsync(5017), "protected local-test HTTP port 5017 is already occupied");
        var database = "mod0354_http_" + Guid.NewGuid().ToString("N");
        var uri = $"mongodb://127.0.0.1:{mongo.Port}/?replicaSet={mongo.ReplicaSetName}&directConnection=true&serverSelectionTimeoutMS=1000";
        var app = Program.BuildLocalTestApp(uri, database);
        await app.Services.GetRequiredService<DwsMongoIndexInitializer>().InitializeAsync();
        await app.StartAsync();
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5017") };
            var identity = app.Services.GetRequiredService<DwsLocalTestIdentityFixture>();
            var contextFixture = app.Services.GetRequiredService<DwsLocalMod0117Fixture>();
            var authFixture = app.Services.GetRequiredService<DwsLocalFu16Fixture>();
            var tenant = Guid.NewGuid();
            var subject = Guid.NewGuid();
            var effective = Guid.NewGuid();
            var delegated = Guid.NewGuid();
            var context = new DwsTrustedActorContext(tenant, subject, effective, delegated, "http-key");
            var reference = DwsFunctionalMongoScope.Reference();
            var request = new CreateStructureRequest(reference, "HTTP", null);

            identity.Configure(new(false, tenant, subject, effective, delegated));
            Assert.Equal(HttpStatusCode.Unauthorized, (await SendCreateAsync(client, request, "unauthorized")).StatusCode);

            identity.Configure(new(true, tenant, subject, effective, delegated));
            ConfigureContext(contextFixture, context, reference, DwsLocalContextDisposition.Accepted);
            ConfigureAuthorization(authFixture, context, DwsLocalAuthorizationDisposition.PermissionDenied);
            Assert.Equal(HttpStatusCode.Forbidden, (await SendCreateAsync(client, request, "forbidden")).StatusCode);

            ConfigureAuthorization(authFixture, context, DwsLocalAuthorizationDisposition.Unavailable);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, (await SendCreateAsync(client, request, "unavailable")).StatusCode);

            ConfigureAuthorization(authFixture, context, DwsLocalAuthorizationDisposition.Accepted);
            ConfigureContext(contextFixture, context, reference, DwsLocalContextDisposition.NotFound);
            Assert.Equal(HttpStatusCode.NotFound, (await SendCreateAsync(client, request, "not-found")).StatusCode);

            ConfigureContext(contextFixture, context, reference, DwsLocalContextDisposition.StaleFence);
            Assert.Equal(HttpStatusCode.Conflict, (await SendCreateAsync(client, request, "conflict")).StatusCode);

            ConfigureContext(contextFixture, context, reference, DwsLocalContextDisposition.Accepted);
            var accepted = await SendCreateAsync(client, request, "accepted");
            Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
            using var envelope = JsonDocument.Parse(await accepted.Content.ReadAsStringAsync());
            var id = envelope.RootElement.GetProperty("data").GetProperty("structureDefinitionId").GetGuid();

            authFixture.Configure(new(
                DwsFunctionalAuthorizationBinding.ModuleCode,
                DwsFunctionalAuthorizationBinding.ModuleEntitlementCode,
                context with { IdempotencyKey = null },
                "GetStructureByIdQuery",
                DwsAuthorizationManifest.RequireExact("GetStructureByIdQuery"),
                true, 1, 1, 1, 1, DwsLocalAuthorizationDisposition.Accepted));
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/dws/structures/{id:D}")).StatusCode);

            using var malformed = new HttpRequestMessage(HttpMethod.Post, "/api/dws/structures")
            {
                Content = JsonContent.Create(new { })
            };
            malformed.Headers.Add(DwsLocalTestAuthenticationDefaults.IdempotencyHeader, "malformed");
            Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(malformed)).StatusCode);
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
            await mongo.Client.DropDatabaseAsync(database);
        }
    }

    private static void ConfigureContext(
        DwsLocalMod0117Fixture fixture,
        DwsTrustedActorContext context,
        ExternalContextReference reference,
        DwsLocalContextDisposition disposition) =>
        fixture.Configure(new(context, reference, 1, disposition));

    private static void ConfigureAuthorization(
        DwsLocalFu16Fixture fixture,
        DwsTrustedActorContext context,
        DwsLocalAuthorizationDisposition disposition) =>
        fixture.Configure(new(
            DwsFunctionalAuthorizationBinding.ModuleCode,
            DwsFunctionalAuthorizationBinding.ModuleEntitlementCode,
            context,
            "CreateStructureCommand",
            DwsAuthorizationManifest.RequireExact("CreateStructureCommand"),
            true, 1, 1, 1, 1, disposition));

    private static async Task<HttpResponseMessage> SendCreateAsync(HttpClient client, CreateStructureRequest request, string key)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/dws/structures") { Content = JsonContent.Create(request) };
        message.Headers.Add(DwsLocalTestAuthenticationDefaults.IdempotencyHeader, key);
        return await client.SendAsync(message);
    }

    private static async Task<HttpResponseMessage> SendRawCreateAsync(HttpClient client, byte[] body, string key)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/dws/structures")
        {
            Content = new ByteArrayContent(body)
        };
        message.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
        message.Headers.Add(DwsLocalTestAuthenticationDefaults.IdempotencyHeader, key);
        return await client.SendAsync(message);
    }

    private static async Task<long> CountTenantDocumentsAsync(IServiceProvider services, Guid tenant)
    {
        var context = services.GetRequiredService<DwsMongoContext>();
        var filter = MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.Filter.Eq(
            "TenantId", new MongoDB.Bson.BsonBinaryData(tenant, MongoDB.Bson.GuidRepresentation.Standard));
        long count = 0;
        foreach (var alias in DwsMongoContext.CollectionAliases.Keys)
            count += await context.Collection(alias).CountDocumentsAsync(filter);
        return count;
    }

    private static async Task<bool> IsOccupiedAsync(int port)
    {
        using var client = new TcpClient();
        try { await client.ConnectAsync(IPAddress.Loopback, port); return true; }
        catch (SocketException) { return false; }
    }
}
