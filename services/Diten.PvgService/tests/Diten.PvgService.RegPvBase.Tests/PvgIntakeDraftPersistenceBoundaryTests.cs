using System.Reflection;
using Diten.PvgService.Application.RegPvBase;
using Diten.PvgService.Domain.RegPvBase;
using Diten.PvgService.Infrastructure.RegPvBase;
using Xunit;

namespace Diten.PvgService.RegPvBase.Tests;

public sealed class PvgIntakeDraftPersistenceBoundaryTests
{
    private static readonly string[] ForbiddenOperationNames =
    [
        "Archive",
        "Void",
        "Export",
        "Delete",
        "BulkDelete"
    ];

    [Fact]
    public async Task Repository_reads_are_tenant_scoped_without_cross_tenant_existence_leak()
    {
        var repository = new InMemoryPvgIntakeDraftRepository();
        var tenantA = new PvgPersistenceTenantScope("tenant-a-secret");
        var tenantB = new PvgPersistenceTenantScope("tenant-b-secret");

        var tenantAId = await repository.AddAsync(tenantA, Intake("tenant-a-secret", PvgIntakeStatus.IntakeCreated));
        var tenantBId = await repository.AddAsync(tenantB, Intake("tenant-b-secret", PvgIntakeStatus.IntakeUpdated));

        var crossTenantRead = await repository.FindByIdAsync(tenantB, tenantAId);
        var tenantBList = await repository.ListAsync(new PvgPersistenceListScope(tenantB, 1, 10, null));
        var tenantAList = await repository.ListAsync(new PvgPersistenceListScope(tenantA, 1, 10, null));

        Assert.Null(crossTenantRead);
        Assert.Single(tenantBList);
        Assert.Equal(tenantBId, tenantBList[0].IntakeDraftId);
        Assert.Single(tenantAList);
        Assert.Equal(tenantAId, tenantAList[0].IntakeDraftId);
    }

    [Fact]
    public async Task Repository_mutations_are_tenant_scoped()
    {
        var repository = new InMemoryPvgIntakeDraftRepository();
        var tenantA = new PvgPersistenceTenantScope("tenant-a-secret");
        var tenantB = new PvgPersistenceTenantScope("tenant-b-secret");
        var tenantAId = await repository.AddAsync(tenantA, Intake("tenant-a-secret", PvgIntakeStatus.IntakeCreated));
        var tenantAVersion = (await repository.FindByIdAsync(tenantA, tenantAId))!.Version;

        var crossTenantReplace = await repository.ReplaceAsync(
            tenantB,
            tenantAId,
            tenantAVersion,
            Intake("tenant-b-secret", PvgIntakeStatus.IntakeUpdated));
        var tenantARead = await repository.FindByIdAsync(tenantA, tenantAId);

        Assert.Equal(PvgIntakeDraftReplaceOutcome.NotFound, crossTenantReplace.Outcome);
        Assert.NotNull(tenantARead);
        Assert.Equal(PvgIntakeStatus.IntakeCreated, tenantARead.Intake.Status);
    }

    [Fact]
    public async Task ReplaceAsync_with_stale_version_returns_conflict_not_not_found()
    {
        var repository = new InMemoryPvgIntakeDraftRepository();
        var tenant = new PvgPersistenceTenantScope("tenant-a-secret");
        var intakeDraftId = await repository.AddAsync(tenant, Intake("tenant-a-secret", PvgIntakeStatus.IntakeCreated));
        var initialVersion = (await repository.FindByIdAsync(tenant, intakeDraftId))!.Version;

        var current = await repository.ReplaceAsync(
            tenant,
            intakeDraftId,
            initialVersion,
            Intake("tenant-a-secret", PvgIntakeStatus.IntakeUpdated));
        var stale = await repository.ReplaceAsync(
            tenant,
            intakeDraftId,
            initialVersion,
            Intake("tenant-a-secret", PvgIntakeStatus.Triaged));
        var persisted = await repository.FindByIdAsync(tenant, intakeDraftId);

        Assert.Equal(PvgIntakeDraftReplaceOutcome.Replaced, current.Outcome);
        Assert.NotNull(current.Version);
        Assert.NotEqual(initialVersion, current.Version);
        Assert.Equal(PvgIntakeDraftReplaceOutcome.VersionConflict, stale.Outcome);
        Assert.NotEqual(PvgIntakeDraftReplaceOutcome.NotFound, stale.Outcome);
        Assert.Null(stale.Version);
        Assert.NotNull(persisted);
        Assert.Equal(PvgIntakeStatus.IntakeUpdated, persisted.Intake.Status);
        Assert.Equal(current.Version, persisted.Version);
    }

    [Fact]
    public async Task Concurrent_replace_with_same_expected_version_allows_exactly_one_success()
    {
        const int rounds = 100;
        const int concurrentCalls = 8;
        var repository = new InMemoryPvgIntakeDraftRepository();
        var tenant = new PvgPersistenceTenantScope("tenant-a-secret");

        for (var round = 0; round < rounds; round++)
        {
            var intakeDraftId = await repository.AddAsync(tenant, Intake("tenant-a-secret", PvgIntakeStatus.IntakeCreated));
            var expectedVersion = (await repository.FindByIdAsync(tenant, intakeDraftId))!.Version;
            using var startBarrier = new Barrier(concurrentCalls);

            var calls = Enumerable.Range(0, concurrentCalls)
                .Select(_ => Task.Factory.StartNew(
                    () =>
                    {
                        Assert.True(startBarrier.SignalAndWait(TimeSpan.FromSeconds(30)));
                        return repository
                            .ReplaceAsync(tenant, intakeDraftId, expectedVersion, Intake("tenant-a-secret", PvgIntakeStatus.IntakeUpdated))
                            .AsTask()
                            .GetAwaiter()
                            .GetResult();
                    },
                    CancellationToken.None,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default))
                .ToArray();
            var results = await Task.WhenAll(calls).WaitAsync(TimeSpan.FromSeconds(60));
            var persisted = await repository.FindByIdAsync(tenant, intakeDraftId);

            Assert.Equal(1, results.Count(result => result.Outcome == PvgIntakeDraftReplaceOutcome.Replaced));
            Assert.Equal(
                concurrentCalls - 1,
                results.Count(result => result.Outcome == PvgIntakeDraftReplaceOutcome.VersionConflict));
            Assert.NotNull(persisted);
            Assert.Equal(
                results.Single(result => result.Outcome == PvgIntakeDraftReplaceOutcome.Replaced).Version,
                persisted.Version);
        }
    }

    [Fact]
    public async Task Repository_rejects_tenant_mismatch_with_safe_reason_code_only()
    {
        var repository = new InMemoryPvgIntakeDraftRepository();
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await repository.AddAsync(
                new PvgPersistenceTenantScope("tenant-a-secret"),
                Intake("tenant-b-secret", PvgIntakeStatus.IntakeCreated)));

        Assert.Equal("PVG_PERSISTENCE_TENANT_SCOPE_MISMATCH", exception.Message);
        Assert.DoesNotContain("tenant-a-secret", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tenant-b-secret", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Persistence_indexes_are_tenant_first_and_do_not_create_global_unique_ids()
    {
        Assert.NotEmpty(PvgIntakeDraftIndexCatalog.RequiredIndexes);

        foreach (var index in PvgIntakeDraftIndexCatalog.RequiredIndexes)
        {
            Assert.NotEmpty(index.Fields);
            Assert.Equal("TenantId", index.Fields[0]);
        }

        Assert.Contains(
            PvgIntakeDraftIndexCatalog.RequiredIndexes,
            index => index.IsUnique &&
                     index.Fields.SequenceEqual(["TenantId", "IntakeDraftId"]));
        Assert.DoesNotContain(
            PvgIntakeDraftIndexCatalog.RequiredIndexes,
            index => index.IsUnique &&
                     index.Fields.SequenceEqual(["IntakeDraftId"]));
    }

    [Fact]
    public void Public_create_and_update_requests_do_not_accept_client_tenant_id()
    {
        AssertNoTenantId(typeof(PvgCreateIntakeDraftRequest));
        AssertNoTenantId(typeof(PvgUpdateIntakeDraftRequest));
    }

    [Fact]
    public void Persistence_boundary_has_no_delete_bulk_delete_archive_void_or_export_methods()
    {
        var persistenceTypes = new[]
            {
                typeof(IPvgIntakeDraftStore),
                typeof(InMemoryPvgIntakeDraftRepository),
                typeof(PvgIntakeDraftIndexCatalog)
            }
            .Concat(typeof(InMemoryPvgIntakeDraftRepository).Assembly.GetTypes()
                .Where(type => type.Namespace == "Diten.PvgService.Infrastructure.RegPvBase"))
            .Distinct()
            .ToArray();

        foreach (var type in persistenceTypes)
        {
            AssertNoForbiddenOperationName(type.Name);
            foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                AssertNoForbiddenOperationName(method.Name);
            }
        }
    }

    private static SafetyCaseIntake Intake(string tenantId, PvgIntakeStatus status) =>
        new(
            tenantId,
            status,
            "Portal",
            "Reporter",
            DateTimeOffset.UnixEpoch,
            "HealthcareProfessional",
            "free text narrative with PHI",
            "Serious",
            "High")
        {
            SourceReference = "source-ref",
            ReporterContactSummary = "reporter@example.test",
            PatientSubjectCode = "patient-subject-code",
            EventOnsetDate = DateOnly.FromDateTime(DateTime.UnixEpoch),
            ProductLines = [new PvgIntakeProductLine("Suspect", null, "suspect product", null, null)],
            EvidenceLinkReferences = ["evidence-ref"]
        };

    private static void AssertNoTenantId(Type contractType)
    {
        var properties = contractType.GetProperties(BindingFlags.Instance | BindingFlags.Public);
        Assert.DoesNotContain(properties, property => property.Name is "TenantId" or "ClientTenantId");
    }

    private static void AssertNoForbiddenOperationName(string name)
    {
        foreach (var forbidden in ForbiddenOperationNames)
        {
            Assert.DoesNotContain(forbidden, name, StringComparison.OrdinalIgnoreCase);
        }
    }
}
