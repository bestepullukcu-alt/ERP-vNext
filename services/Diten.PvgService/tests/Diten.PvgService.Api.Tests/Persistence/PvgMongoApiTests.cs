using System.Text.Json.Nodes;
using Diten.BuildingBlocks.Security.Secrets;
using Diten.PvgService.Application.RegPvBase;
using Diten.PvgService.Infrastructure.RegPvBase;
using Diten.PvgService.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Diten.PvgService.Api.Tests.Persistence;

// WP-0230-DL10, AC-35 (b) second test, (c), (c2), AC-13 (c) store selection and (f): the API host with
// Pvg:Persistence:Store=Mongo, the allow stubs in place of the four deny-all adapters, and signed tenants. The host is
// started for real on a random loopback port; the shared replica set is the store.
[Collection(PvgSharedMongoCollection.Name)]
public sealed class PvgMongoApiTests(PvgMongoReplicaSetFixture mongo)
{
    private const string Path = PvgCaseIntakeTriageEndpoints.RoutePrefix;
    private const string SubjectA = "actor-sub-a-fake";
    private const string SubjectB = "actor-sub-b-fake";
    private const string ForgedActor = "actor-forged-fake";

    private static (string Name, string Value)[] ForgedActorHeaders() =>
    [
        ("X-Diten-Actor-Id", ForgedActor),
        ("X-Diten-Actor-Kind", "platform_admin")
    ];

    // ---- AC-35 (b): cross-tenant access looks like an unknown id --------------------------------------------------

    [Fact]
    public async Task Cross_tenant_get_and_put_on_the_mongo_store_answer_like_an_unknown_id()
    {
        await using var host = await PvgMongoApiHost.StartMongoAsync(mongo.ConnectionString);
        var tenantA = PvgMongoTestData.NewTenant();
        var tenantB = PvgMongoTestData.NewTenant();

        // One PUT body for every PUT below. Field validation runs before the store is asked, so an invalid body would
        // answer the same for a foreign draft, an unknown id and the owner's own draft, and this test would stay green
        // without the store ever being reached. The last step of the test therefore sends this same body for the owner.
        var putBody = PvgMongoApiHost.UpdateJson(1);

        var created = await host.SendAsync(HttpMethod.Post, Path, tenantA, SubjectA, PvgMongoApiHost.CreateJson);
        Assert.Equal(200, created.StatusCode);
        var draftId = created.Body().IntakeDraftId!;

        // Positive control first: the owning tenant's GET returns 200 with the draft.
        var owner = await host.SendAsync(HttpMethod.Get, $"{Path}/{draftId}", tenantA, SubjectA);
        Assert.Equal(200, owner.StatusCode);
        Assert.Equal(draftId, owner.Body().Draft!.IntakeDraftId);

        var unknownId = "pvg-draft-" + Guid.NewGuid().ToString("N");
        var unknownGet = await host.SendAsync(HttpMethod.Get, $"{Path}/{unknownId}", tenantB, SubjectB);
        var unknownPut = await host.SendAsync(
            HttpMethod.Put, $"{Path}/{unknownId}", tenantB, SubjectB, putBody);

        var crossGet = await host.SendAsync(HttpMethod.Get, $"{Path}/{draftId}", tenantB, SubjectB);
        var crossPut = await host.SendAsync(
            HttpMethod.Put, $"{Path}/{draftId}", tenantB, SubjectB, putBody);

        // The other tenant's GET and PUT answer like the unknown id: same status, same body, no record data.
        // What that status is today: 409 with the reason code PVG_INTAKE_DRAFT_NOT_FOUND. The pack's target status is
        // 404 (AC-6 / OD-PVG-3, slice DL-4C, not delivered; gap G-8); PvgCaseIntakeTriageEndpoints.cs answers every
        // not-found with 409 until then, and this slice may not change that file or the API contract. The assertion on
        // 409 below is the one line that changes when DL-4C lands.
        Assert.Equal(409, unknownGet.StatusCode);
        Assert.Equal(409, unknownPut.StatusCode);
        foreach (var (cross, unknown) in new[] { (crossGet, unknownGet), (crossPut, unknownPut) })
        {
            Assert.Equal(unknown.StatusCode, cross.StatusCode);
            Assert.Equal(unknown.Raw, cross.Raw);
            Assert.Equal("PVG_INTAKE_DRAFT_NOT_FOUND", cross.Body().ReasonCode);
            Assert.DoesNotContain(draftId, cross.Raw, StringComparison.Ordinal);
            Assert.DoesNotContain("narrative-fake", cross.Raw, StringComparison.Ordinal);
        }

        // The PUT of the other tenant changed nothing.
        var after = await host.SendAsync(HttpMethod.Get, $"{Path}/{draftId}", tenantA, SubjectA);
        Assert.Equal(200, after.StatusCode);
        Assert.Equal(1, after.Body().Draft!.Version);

        // The same PUT body is valid: the owner's own PUT succeeds with it, so the refusals above came from the store's
        // tenant filter and not from the body.
        var ownerPut = await host.SendAsync(HttpMethod.Put, $"{Path}/{draftId}", tenantA, SubjectA, putBody);
        Assert.Equal(200, ownerPut.StatusCode);
        Assert.Equal(2, ownerPut.Body().Version);
    }

    // ---- AC-35 (c): durability --------------------------------------------------------------------------------------

    [Fact]
    public async Task Draft_survives_a_host_restart_on_the_mongo_store()
    {
        var tenant = PvgMongoTestData.NewTenant();
        string draftId;
        long version;

        await using (var first = await PvgMongoApiHost.StartMongoAsync(mongo.ConnectionString))
        {
            var created = await first.SendAsync(HttpMethod.Post, Path, tenant, SubjectA, PvgMongoApiHost.CreateJson);
            Assert.Equal(200, created.StatusCode);
            draftId = created.Body().IntakeDraftId!;

            var updated = await first.SendAsync(
                HttpMethod.Put, $"{Path}/{draftId}", tenant, SubjectA, PvgMongoApiHost.UpdateJson(1));
            Assert.Equal(200, updated.StatusCode);
            version = updated.Body().Version!.Value;
            Assert.Equal(2, version);
        }

        await using var second = await PvgMongoApiHost.StartMongoAsync(mongo.ConnectionString);
        var read = await second.SendAsync(HttpMethod.Get, $"{Path}/{draftId}", tenant, SubjectA);

        Assert.Equal(200, read.StatusCode);
        var draft = read.Body().Draft!;
        Assert.Equal(draftId, draft.IntakeDraftId);
        Assert.Equal(version, draft.Version);
        Assert.Equal("Spontaneous", draft.ReportType);
    }

    // ---- AC-35 (c2): actor fields ---------------------------------------------------------------------------------

    [Fact]
    public async Task Mongo_store_writes_CreatedBy_and_UpdatedBy_from_the_validated_subject()
    {
        await using var host = await PvgMongoApiHost.StartMongoAsync(mongo.ConnectionString);
        var tenant = PvgMongoTestData.NewTenant();

        // Subject A creates, with a forged actor header of a third value.
        var created = await host.SendAsync(
            HttpMethod.Post, Path, tenant, SubjectA, PvgMongoApiHost.CreateJson, ForgedActorHeaders());
        Assert.Equal(200, created.StatusCode);
        var draftId = created.Body().IntakeDraftId!;

        var afterCreate = (await mongo.RawDraftAsync(tenant, draftId))!;
        Assert.Equal(SubjectA, afterCreate["CreatedBy"].AsString);
        Assert.Equal(SubjectA, afterCreate["UpdatedBy"].AsString);

        // Subject B of the same tenant updates, again with a forged header.
        var updated = await host.SendAsync(
            HttpMethod.Put, $"{Path}/{draftId}", tenant, SubjectB, PvgMongoApiHost.UpdateJson(1), ForgedActorHeaders());
        Assert.Equal(200, updated.StatusCode);

        var afterUpdate = (await mongo.RawDraftAsync(tenant, draftId))!;
        Assert.Equal(SubjectA, afterUpdate["CreatedBy"].AsString);
        Assert.Equal(SubjectB, afterUpdate["UpdatedBy"].AsString);
        Assert.NotEqual(ForgedActor, afterUpdate["CreatedBy"].AsString);
        Assert.NotEqual(ForgedActor, afterUpdate["UpdatedBy"].AsString);
        Assert.Equal(2, afterUpdate["Version"].AsInt64);
    }

    [Fact]
    public void Create_and_update_requests_do_not_accept_CreatedBy_or_UpdatedBy()
    {
        // The application requests and the HTTP body records carry neither property.
        Assert.Empty(PvgActorFieldCheck.FindActorFieldProperties(
        [
            typeof(PvgCreateIntakeDraftRequest),
            typeof(PvgUpdateIntakeDraftRequest),
            typeof(PvgCaseIntakeCreateRequest),
            typeof(PvgCaseIntakeUpdateRequest)
        ]));

        // The check itself: two fixture records, one carrying only CreatedBy and one only UpdatedBy.
        var findings = PvgActorFieldCheck.FindActorFieldProperties(
            [typeof(FixtureWithCreatedBy), typeof(FixtureWithUpdatedBy)]);

        Assert.Equal(
            new[] { nameof(FixtureWithCreatedBy) + ".CreatedBy", nameof(FixtureWithUpdatedBy) + ".UpdatedBy" },
            findings.Order(StringComparer.Ordinal).ToArray());
    }

    private sealed record FixtureWithCreatedBy(string? Name, string? CreatedBy);

    private sealed record FixtureWithUpdatedBy(string? Name, string? UpdatedBy);

    [Fact]
    public async Task Body_createdBy_and_updatedBy_never_reach_the_store()
    {
        await using var host = await PvgMongoApiHost.StartMongoAsync(mongo.ConnectionString);
        var tenant = PvgMongoTestData.NewTenant();

        // A body carrying createdBy, with a token of subject A.
        var created = await host.SendAsync(
            HttpMethod.Post,
            Path,
            tenant,
            SubjectA,
            PvgMongoApiHost.CreateJsonWith(("createdBy", JsonValue.Create("body-created-by-fake"))));
        Assert.Equal(200, created.StatusCode);
        var draftId = created.Body().IntakeDraftId!;

        var afterCreate = (await mongo.RawDraftAsync(tenant, draftId))!;
        Assert.Equal(SubjectA, afterCreate["CreatedBy"].AsString);
        Assert.Equal(SubjectA, afterCreate["UpdatedBy"].AsString);

        // Then a body carrying updatedBy, with a token of subject B.
        var updated = await host.SendAsync(
            HttpMethod.Put,
            $"{Path}/{draftId}",
            tenant,
            SubjectB,
            PvgMongoApiHost.UpdateJson(1, ("updatedBy", JsonValue.Create("body-updated-by-fake"))));
        Assert.Equal(200, updated.StatusCode);

        var afterUpdate = (await mongo.RawDraftAsync(tenant, draftId))!;
        Assert.Equal(SubjectA, afterUpdate["CreatedBy"].AsString);
        Assert.Equal(SubjectB, afterUpdate["UpdatedBy"].AsString);
        Assert.NotEqual("body-created-by-fake", afterUpdate["CreatedBy"].AsString);
        Assert.NotEqual("body-updated-by-fake", afterUpdate["UpdatedBy"].AsString);
    }

    // WP-0230-DL10-R1, J9 and J10: the triage and route writes carry the validated subject as UpdatedBy as well. (The
    // pack asks for it on create and update; these two calls hand the actor to the store the same way, and a constant in
    // either call would not show in any other test.)
    [Theory]
    [InlineData("triage")]
    [InlineData("route")]
    public async Task Mongo_store_writes_UpdatedBy_from_the_validated_subject_on_triage_and_route(string operation)
    {
        await using var host = await PvgMongoApiHost.StartMongoAsync(mongo.ConnectionString);
        var tenant = PvgMongoTestData.NewTenant();

        var created = await host.SendAsync(HttpMethod.Post, Path, tenant, SubjectA, PvgMongoApiHost.CreateJson);
        Assert.Equal(200, created.StatusCode);
        var draftId = created.Body().IntakeDraftId!;

        var body = operation == "triage"
            ? """{"triageOutcome":1,"triageReason":"triage-reason-fake","expectedVersion":1}"""
            : """{"routeTargetQueue":"queue-fake","expectedVersion":1}""";
        var written = await host.SendAsync(
            HttpMethod.Post, $"{Path}/{draftId}/{operation}", tenant, SubjectB, body, ForgedActorHeaders());

        Assert.Equal(200, written.StatusCode);
        Assert.Equal(2, written.Body().Version);
        var raw = (await mongo.RawDraftAsync(tenant, draftId))!;
        Assert.Equal(SubjectA, raw["CreatedBy"].AsString);
        Assert.Equal(SubjectB, raw["UpdatedBy"].AsString);
        Assert.Equal(2, raw["Version"].AsInt64);
        Assert.Equal(operation == "triage" ? "Triaged" : "RoutePending", raw["Status"].AsString);
    }

    // ---- AC-13 (c): which store the host selects ------------------------------------------------------------------

    // WP-0230-DL10-R1: the connection string is read through the secrets provider, not from the configuration. The
    // configuration holds a value that reaches no server; the replaced provider knows the real replica set. A store that
    // read the configuration directly would try the closed address and the host would refuse to start.
    [Fact]
    public async Task Mongo_connection_string_is_read_through_the_secrets_provider()
    {
        var provider = new RecordingSecretsProvider(mongo.ConnectionString);
        var configuration = PvgMongoApiHost.Configuration(
            "Mongo", "mongodb://127.0.0.1:1/?directConnection=true", PvgMongoTestHarness.SharedDatabaseName);

        await using (var host = await PvgMongoApiHost.StartAsync(
                         configuration,
                         services => services.Replace(ServiceDescriptor.Singleton<ISecretsProvider>(provider))))
        {
            var tenant = PvgMongoTestData.NewTenant();
            var created = await host.SendAsync(HttpMethod.Post, Path, tenant, SubjectA, PvgMongoApiHost.CreateJson);
            Assert.Equal(200, created.StatusCode);
            var read = await host.SendAsync(HttpMethod.Get, $"{Path}/{created.Body().IntakeDraftId}", tenant, SubjectA);
            Assert.Equal(200, read.StatusCode);
        }

        Assert.Contains(PvgPersistenceServiceCollectionExtensions.ConnectionStringKey, provider.RequestedKeys);

        // The provider exists only when Mongo is selected; the in-memory host registers none.
        using var inMemory = PvgMongoApiHost.Build(PvgMongoApiHost.Configuration("InMemory", null, null));
        Assert.Null(inMemory.Services.GetService<ISecretsProvider>());

        // A missing connection string refuses the start, and the message names the key and nothing else.
        var missing = PvgMongoApiHost.Configuration("Mongo", null, PvgMongoTestHarness.SharedDatabaseName);
        var exception = Assert.ThrowsAny<Exception>(() => PvgMongoApiHost.Build(missing));
        Assert.Contains(PvgPersistenceServiceCollectionExtensions.ConnectionStringKey, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(PvgMongoTestHarness.SharedDatabaseName, exception.Message, StringComparison.Ordinal);
    }

    private sealed class RecordingSecretsProvider(string connectionString) : ISecretsProvider
    {
        private readonly List<string> _requestedKeys = [];

        public IReadOnlyList<string> RequestedKeys
        {
            get
            {
                lock (_requestedKeys)
                {
                    return _requestedKeys.ToArray();
                }
            }
        }

        public Task<string> GetSecretAsync(string key, CancellationToken ct)
        {
            lock (_requestedKeys)
            {
                _requestedKeys.Add(key);
            }

            return Task.FromResult(
                key == PvgPersistenceServiceCollectionExtensions.ConnectionStringKey
                    ? connectionString
                    : throw new InvalidOperationException("Unexpected secret key."));
        }

        public Task<IReadOnlyDictionary<string, string>> GetSecretsAsync(string prefix, CancellationToken ct) =>
            throw new InvalidOperationException("Unexpected secret prefix.");
    }


    [Fact]
    public void In_memory_store_is_the_default_and_mongo_is_opt_in()
    {
        using (var withoutKey = PvgMongoApiHost.Build(PvgMongoApiHost.Configuration(null, null, null)))
        {
            Assert.IsType<InMemoryPvgIntakeDraftRepository>(withoutKey.Services.GetRequiredService<IPvgIntakeDraftStore>());
        }

        using (var inMemory = PvgMongoApiHost.Build(PvgMongoApiHost.Configuration("InMemory", null, null)))
        {
            Assert.IsType<InMemoryPvgIntakeDraftRepository>(inMemory.Services.GetRequiredService<IPvgIntakeDraftStore>());
        }

        using var selected = PvgMongoApiHost.Build(PvgMongoApiHost.Configuration(
            "Mongo", mongo.ConnectionString, PvgMongoTestHarness.SharedDatabaseName));
        Assert.IsType<PvgMongoIntakeDraftStore>(selected.Services.GetRequiredService<IPvgIntakeDraftStore>());
    }

    // ---- AC-13 (f): hosted services -----------------------------------------------------------------------------------

    [Fact]
    public void Pvg_host_registers_only_the_named_hosted_services()
    {
        // Measured on the host at Expected HEAD (DL-10 DoR): these two, and no other. DL-11 (AC-37) adds exactly one
        // type, the closed catalog registration, and nothing else.
        string[] baseline =
        [
            "Microsoft.AspNetCore.DataProtection.Internal.DataProtectionHostedService",
            "Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckPublisherHostedService",
            "Diten.PvgService.Api.ModuleRegistration.PvgModuleRegistrationHostedService"
        ];

        using (var inMemory = PvgMongoApiHost.Build(PvgMongoApiHost.Configuration("InMemory", null, null)))
        {
            Assert.Equal(
                baseline.Order(StringComparer.Ordinal),
                HostedServiceTypeNames(inMemory).Order(StringComparer.Ordinal));
        }

        using var selected = PvgMongoApiHost.Build(PvgMongoApiHost.Configuration(
            "Mongo", mongo.ConnectionString, PvgMongoTestHarness.SharedDatabaseName));
        Assert.Equal(
            baseline.Append(typeof(PvgMongoSchemaBootstrap).FullName!).Order(StringComparer.Ordinal),
            HostedServiceTypeNames(selected).Order(StringComparer.Ordinal));

        // No PvgService type is a background service.
        var assemblies = new[]
        {
            typeof(Diten.PvgService.Domain.RegPvBase.SafetyCaseIntake).Assembly,
            typeof(IPvgIntakeDraftStore).Assembly,
            typeof(InMemoryPvgIntakeDraftRepository).Assembly,
            typeof(PvgMongoIntakeDraftStore).Assembly,
            typeof(PvgServiceApiHost).Assembly
        };
        var backgroundServices = assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => typeof(BackgroundService).IsAssignableFrom(type))
            .Select(type => type.FullName)
            .ToArray();
        Assert.Empty(backgroundServices);
    }

    private static string[] HostedServiceTypeNames(Microsoft.AspNetCore.Hosting.IWebHost host) =>
        host.Services.GetServices<IHostedService>().Select(service => service.GetType().FullName!).ToArray();
}

// Pure: takes types and returns every property named CreatedBy or UpdatedBy (any casing) as "Type.Property".
internal static class PvgActorFieldCheck
{
    private static readonly string[] ActorFieldNames = ["CreatedBy", "UpdatedBy"];

    public static IReadOnlyList<string> FindActorFieldProperties(IEnumerable<Type> types) =>
        types
            .SelectMany(type => type
                .GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public |
                               System.Reflection.BindingFlags.NonPublic)
                .Where(property => ActorFieldNames.Contains(property.Name, StringComparer.OrdinalIgnoreCase))
                .Select(property => type.Name + "." + property.Name))
            .ToArray();
}
