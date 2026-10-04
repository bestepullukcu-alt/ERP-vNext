using System.Net;
using System.Text.Json;
using Diten.Platform.API.Controllers.Internal;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.PlatformAdministrators;

/// <summary>
/// BL-521 FIX2 item 2 — THE PLATFORM HALF OF THE STATUS WIRE: <c>GET /api/internal/platform-administrators/status</c>, the
/// answer Auth mints every platform token's actor type from (login, forced password change, refresh). Measured over HTTP
/// on the production controller and the production repository over a test-owned replica set. Before this file nothing
/// called the endpoint, so it could answer every record as "PlatformAdmin" with every test green.
///
/// <para>The JSON names asserted here (<c>actorType</c>, <c>isActive</c>, the 404 envelope) are the ones Auth's
/// <c>PlatformAdministratorStatusClientWireTests</c> feed the production client.</para>
/// </summary>
[Collection(DisposableMongoReplicaSetCollection.Name)]
public sealed class InternalPlatformAdministratorStatusEndpointTests
{
    private const string TestOnlyInternalKey = "bl521-fix2-test-only-internal-key-platform";

    [Theory]
    [InlineData(ActorType.PartnerAdmin, "PartnerAdmin")]
    [InlineData(ActorType.PlatformAdmin, "PlatformAdmin")]
    public async Task An_active_record_answers_its_own_actor_type(ActorType type, string expected)
    {
        await using var host = await Host.StartAsync();
        var email = await host.SeedAsync(type, AdministratorStatus.Active);

        var (status, root) = await host.GetAsync(email);

        Assert.Equal(HttpStatusCode.OK, status);
        var data = root.GetProperty("data");
        Assert.Equal(expected, data.GetProperty("actorType").GetString());
        Assert.True(data.GetProperty("isActive").GetBoolean());
        Assert.Equal("Active", data.GetProperty("status").GetString());
        // FIX4 item 2 — the record's roles, which Auth converges its own copy to at every sign-in and refresh
        Assert.Equal(["SupportAdmin"], data.GetProperty("roles").EnumerateArray().Select(role => role.GetString()));
    }

    [Theory]
    [InlineData(AdministratorStatus.Suspended)]
    [InlineData(AdministratorStatus.Disabled)]
    public async Task An_inactive_record_answers_not_active_with_its_status_and_type(AdministratorStatus state)
    {
        await using var host = await Host.StartAsync();
        var email = await host.SeedAsync(ActorType.PartnerAdmin, state);

        var (status, root) = await host.GetAsync(email);

        Assert.Equal(HttpStatusCode.OK, status);
        var data = root.GetProperty("data");
        Assert.False(data.GetProperty("isActive").GetBoolean());
        Assert.Equal(state.ToString(), data.GetProperty("status").GetString());
        Assert.Equal("PartnerAdmin", data.GetProperty("actorType").GetString());
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("deleted")]
    public async Task A_missing_or_deleted_record_answers_platforms_404_envelope(string state)
    {
        await using var host = await Host.StartAsync();
        var email = state == "missing"
            ? $"nobody.{Guid.NewGuid():N}@bl521.test"
            : await host.SeedAsync(ActorType.PlatformAdmin, AdministratorStatus.Active, deleted: true);

        var (status, root) = await host.GetAsync(email);

        Assert.Equal(HttpStatusCode.NotFound, status);
        Assert.False(root.GetProperty("isSuccessful").GetBoolean());
        Assert.Equal(404, root.GetProperty("statusCode").GetInt32());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("data").ValueKind);
    }

    [Fact]
    public async Task Without_the_internal_key_nothing_is_answered()
    {
        await using var host = await Host.StartAsync();
        var email = await host.SeedAsync(ActorType.PlatformAdmin, AdministratorStatus.Active);

        var (status, _) = await host.GetAsync(email, internalKey: "not-the-key");

        Assert.Equal(HttpStatusCode.Unauthorized, status);
    }

    private sealed class Host : IAsyncDisposable
    {
        private readonly DisposableMongoReplicaSet _mongo;
        private readonly IMongoDatabase _database;
        private readonly TestServer _server;

        private Host(DisposableMongoReplicaSet mongo, IMongoDatabase database)
        {
            _mongo = mongo;
            _database = database;
            var dbContext = new PlatformDbContext(mongo.Client, database);
            var builder = new WebHostBuilder()
                .UseEnvironment("Test")
                .ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AuthService:InternalApiKey"] = TestOnlyInternalKey
                }))
                .ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddLogging();
                    services.AddAuthorization();
                    services.AddSingleton<IPlatformDbContext>(dbContext);
                    services.AddScoped<ITenantContext, TenantContext>();
                    services.AddScoped<IPlatformAdministratorRepository, PlatformAdministratorRepository>();
                    services.AddControllers().ConfigureApplicationPartManager(manager =>
                    {
                        manager.ApplicationParts.Clear();
                        manager.ApplicationParts.Add(new AssemblyPart(typeof(InternalPlatformAdministratorsController).Assembly));
                        manager.FeatureProviders.Add(new OnlyStatusController());
                    });
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                });
            _server = new TestServer(builder);
        }

        public static async Task<Host> StartAsync()
        {
            var mongo = await DisposableMongoReplicaSet.StartAsync();
            var database = mongo.CreateDatabase();
            foreach (var collection in PlatformSchemaManifest.For(Enum.GetValues<SchemaProfile>())
                         .Where(c => c.Name == PlatformCollections.PlatformAdministrators))
            {
                await collection.ApplyAsync(database, CancellationToken.None);
            }

            return new Host(mongo, database);
        }

        public async Task<string> SeedAsync(ActorType type, AdministratorStatus status, bool deleted = false)
        {
            var email = $"status.{Guid.NewGuid():N}@bl521.test";
            await _database.GetCollection<PlatformAdministrator>(PlatformCollections.PlatformAdministrators).InsertOneAsync(new PlatformAdministrator
            {
                Email = email, NormalizedEmail = email, UserName = email, NormalizedUserName = email, DisplayName = "Status Probe",
                ActorType = type, PartnerId = type == ActorType.PartnerAdmin ? Guid.NewGuid() : null,
                AllowedTenantIds = type == ActorType.PartnerAdmin ? [Guid.NewGuid()] : [],
                Roles = [AdministratorRole.SupportAdmin], Status = status, IsDeleted = deleted,
                InvitationStatus = AdministratorInvitationStatus.Accepted
            });
            return email;
        }

        public async Task<(HttpStatusCode Status, JsonElement Root)> GetAsync(string email, string internalKey = TestOnlyInternalKey)
        {
            using var client = _server.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/internal/platform-administrators/status?email={Uri.EscapeDataString(email)}");
            request.Headers.Add("X-Internal-Api-Key", internalKey);
            using var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            return (response.StatusCode, string.IsNullOrWhiteSpace(body) ? default : JsonDocument.Parse(body).RootElement.Clone());
        }

        public ValueTask DisposeAsync()
        {
            _server.Dispose();
            return _mongo.DisposeAsync();
        }

        private sealed class OnlyStatusController : ControllerFeatureProvider
        {
            protected override bool IsController(System.Reflection.TypeInfo typeInfo) =>
                typeInfo.AsType() == typeof(InternalPlatformAdministratorsController);
        }
    }
}
