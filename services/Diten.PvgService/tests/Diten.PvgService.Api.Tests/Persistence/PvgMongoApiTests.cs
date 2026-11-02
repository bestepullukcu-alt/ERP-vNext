using System.Text.Json.Nodes;
using Diten.PvgService.Application.RegPvBase;
using Diten.PvgService.Infrastructure.RegPvBase;
using Diten.PvgService.Persistence;
using Microsoft.Extensions.DependencyInjection;
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
    public async Task Cross_tenant_get_and_put_on_the_mongo_store_return_404_like_an_unknown_id()
    {
        await using var host = await PvgMongoApiHost.StartMongoAsync(mongo.ConnectionString);
        var tenantA = PvgMongoTestData.NewTenant();
        var tenantB = PvgMongoTestData.NewTenant();

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
            HttpMethod.Put, $"{Path}/{unknownId}", tenantB, SubjectB, PvgMongoApiHost.UpdateJson(1));

        var crossGet = await host.SendAsync(HttpMethod.Get, $"{Path}/{draftId}", tenantB, SubjectB);
        var crossPut = await host.SendAsync(
            HttpMethod.Put, $"{Path}/{draftId}", tenantB, SubjectB, PvgMongoApiHost.UpdateJson(1));

        // The other tenant's GET and PUT are the same as the unknown id's: same status, same body, no record data.
        // DEVIATION D-1 (recorded in the work report): the pack's target status is 404 (AC-6 / OD-PVG-3, slice DL-4C,
        // not delivered; gap G-8). The host answers every not-found with 409 and the reason code
        // PVG_INTAKE_DRAFT_NOT_FOUND today, and this slice may not change the API contract. The test therefore pins what
        // the pack's sentence is about - cross-tenant looks exactly like unknown - and not the number 404.
        foreach (var (cross, unknown) in new[] { (crossGet, unknownGet), (crossPut, unknownPut) })
        {
            Assert.NotEqual(200, cross.StatusCode);
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

    // ---- AC-13 (c): which store the host selects ------------------------------------------------------------------

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
        // Measured on the host at Expected HEAD (DL-10 DoR): these two, and no other.
        string[] baseline =
        [
            "Microsoft.AspNetCore.DataProtection.Internal.DataProtectionHostedService",
            "Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckPublisherHostedService"
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
