using System.Text.Json.Nodes;
using Diten.Platform.API.Configuration;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Workflow.Operations;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Tests.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Workflow;

/// <summary>
/// WP-PLT-WORKFLOW-TEMPLATES-01 — the operator's <c>--ensure-workflow-templates</c> command, run through its REAL wiring
/// (<see cref="WorkflowTemplatesOperationalMode.BuildServices"/>: the host's configuration, AddApplication + AddInfrastructure,
/// the definitions' own Create / Publish commands) on the test process's own mongod (DITEN_TEST_MONGOD, never 27017), one
/// fixed-name database per test, emptied first.
/// </summary>
public sealed class EnsureWorkflowTemplatesCommandMongoTests
{
    private const string OperatorEnvironment = "Staging";
    private const string DifferentDefinition = """{"stages":[{"code":"tenant","name":"Tenant","steps":[{"code":"decide","name":"Decide","type":"approval"}]}]}""";
    private static readonly string CatalogPath = Path.Combine(AppContext.BaseDirectory, WorkflowTemplateCatalog.RelativePath);
    private static readonly WorkflowTemplateCatalog Catalog = WorkflowTemplateCatalog.Load(CatalogPath);

    // (a) an empty tenant: --apply creates and publishes the seven, as System, with the catalog's reason; a second --apply
    // writes nothing.
    [Fact]
    public async Task Apply_on_an_empty_tenant_creates_and_publishes_the_seven_and_a_second_apply_writes_nothing()
    {
        Assert.True(File.Exists(CatalogPath), CatalogPath); // the catalog is in the build output
        await using var world = await World.StartAsync("wf_templates_apply");
        var tenant = await world.ActiveTenantAsync();

        var (exit, output, error) = await world.RunAsync("--tenant", tenant.ToString(), "--apply");

        Assert.True(exit == EnsureWorkflowTemplatesCommandLine.Done, error);
        Assert.Equal(7, Catalog.Templates.Count);
        foreach (var entry in Catalog.Templates)
        {
            Assert.Contains($"{entry.TemplateCode}", output);
            var (template, versions) = await world.DefinitionAsync(tenant, entry.TemplateCode);
            Assert.NotNull(template);
            Assert.Equal(WorkflowTemplateStatus.Published, template!.Status);
            var version = Assert.Single(versions);
            Assert.Equal(template.ActivePublishedVersionId, version.Id);
            Assert.Equal("system", version.PublishedBy);
            Assert.Equal($"WP-PLT-WORKFLOW-TEMPLATES-01 catalog v{Catalog.CatalogVersion}", version.PublishReason);
            Assert.True(entry.Matches(version.DefinitionJson, version.SchemaVersion, version.ExpressionVersion));
        }

        Assert.Equal(7, CountOf(output, EnsureWorkflowTemplatesCommandLine.CreatedAndPublished));
        var before = await world.SnapshotAsync();

        var (secondExit, secondOutput, secondError) = await world.RunAsync("--tenant", tenant.ToString(), "--apply");

        Assert.True(secondExit == EnsureWorkflowTemplatesCommandLine.Done, secondError);
        Assert.Equal(7, CountOf(secondOutput, EnsureWorkflowTemplatesCommandLine.AlreadyPublished));
        Assert.Equal(before, await world.SnapshotAsync()); // documents and versions unchanged
    }

    // (b) a Draft definition with no version is only published (no second definition is created).
    [Fact]
    public async Task A_draft_without_a_version_is_only_published()
    {
        await using var world = await World.StartAsync("wf_templates_draft");
        var tenant = await world.ActiveTenantAsync();
        var code = Catalog.Templates[0].TemplateCode;
        var draft = await world.SeedTemplateAsync(tenant, code, WorkflowTemplateStatus.Draft);

        var (exit, output, error) = await world.RunAsync("--tenant", tenant.ToString(), "--apply");

        Assert.True(exit == EnsureWorkflowTemplatesCommandLine.Done, error);
        Assert.Contains($"{code,-28} {EnsureWorkflowTemplatesCommandLine.Published}", output);
        var (template, versions) = await world.DefinitionAsync(tenant, code);
        Assert.Equal(draft.Id, template!.Id); // the same definition, published
        Assert.Equal(WorkflowTemplateStatus.Published, template.Status);
        Assert.Single(versions);
        Assert.Equal(1, await world.TemplateCountAsync(tenant, code));
    }

    // (b2) a definition someone designed — Draft with a version, Superseded, Archived, or Published with no active version —
    // is left exactly as it is: needs-owner, exit 2.
    [Theory]
    [InlineData("draft-with-version", "wf_templates_owner_draft")]
    [InlineData("superseded", "wf_templates_owner_superseded")]
    [InlineData("archived", "wf_templates_owner_archived")]
    [InlineData("published-without-active", "wf_templates_owner_noactive")]
    public async Task A_definition_designed_by_the_tenant_is_left_as_it_is_and_needs_the_owner(string shape, string database)
    {
        await using var world = await World.StartAsync(database);
        var tenant = await world.ActiveTenantAsync();
        var code = Catalog.Templates[1].TemplateCode;
        var status = shape switch
        {
            "superseded" => WorkflowTemplateStatus.Superseded,
            "archived" => WorkflowTemplateStatus.Archived,
            "published-without-active" => WorkflowTemplateStatus.Published,
            _ => WorkflowTemplateStatus.Draft
        };
        var seeded = await world.SeedTemplateAsync(tenant, code, status);
        if (shape == "draft-with-version")
        {
            await world.SeedVersionAsync(tenant, seeded.Id, DifferentDefinition, activate: false);
        }

        var before = await world.RawDefinitionAsync(tenant, code);

        var (exit, output, _) = await world.RunAsync("--tenant", tenant.ToString(), "--apply");

        Assert.Equal(EnsureWorkflowTemplatesCommandLine.NeedsAttention, exit);
        Assert.Contains($"{code,-28} {EnsureWorkflowTemplatesCommandLine.NeedsOwner}", output);
        Assert.Equal(before, await world.RawDefinitionAsync(tenant, code));
    }

    // (c) a published definition that differs from the catalog is left byte for byte: drift, exit 2.
    [Fact]
    public async Task A_published_definition_different_from_the_catalog_is_left_byte_identical_and_reported_as_drift()
    {
        await using var world = await World.StartAsync("wf_templates_drift");
        var tenant = await world.ActiveTenantAsync();
        var code = Catalog.Templates[2].TemplateCode;
        var seeded = await world.SeedTemplateAsync(tenant, code, WorkflowTemplateStatus.Draft);
        await world.SeedVersionAsync(tenant, seeded.Id, DifferentDefinition, activate: true);
        var before = await world.RawDefinitionAsync(tenant, code);

        var (exit, output, _) = await world.RunAsync("--tenant", tenant.ToString(), "--apply");

        Assert.Equal(EnsureWorkflowTemplatesCommandLine.NeedsAttention, exit);
        Assert.Contains($"{code,-28} {EnsureWorkflowTemplatesCommandLine.Drift}", output);
        Assert.Equal(before, await world.RawDefinitionAsync(tenant, code));
    }

    // The same definition in another key order / spacing is the catalog's: already-published, not drift.
    [Fact]
    public async Task A_published_definition_equal_up_to_key_order_and_spacing_is_already_published()
    {
        await using var world = await World.StartAsync("wf_templates_canonical");
        var tenant = await world.ActiveTenantAsync();
        var entry = Catalog.Templates[3];
        var seeded = await world.SeedTemplateAsync(tenant, entry.TemplateCode, WorkflowTemplateStatus.Draft);
        var reordered = """
            { "stages" : [ { "steps": [ { "type":"approval", "name":"Approve", "code":"approve" } ], "name":"Review", "code":"review" } ] }
            """;
        Assert.True(entry.Matches(reordered, entry.SchemaVersion, entry.ExpressionVersion)); // non-vacuity
        await world.SeedVersionAsync(tenant, seeded.Id, reordered, activate: true);

        var (exit, output, error) = await world.RunAsync("--tenant", tenant.ToString(), "--apply");

        Assert.True(exit == EnsureWorkflowTemplatesCommandLine.Done, error);
        Assert.Contains($"{entry.TemplateCode,-28} {EnsureWorkflowTemplatesCommandLine.AlreadyPublished}", output);
    }

    // (d) the platform system tenant (seeded Active, so ONLY the system-tenant rule refuses it), an unknown tenant, a
    // suspended tenant, or no --tenant: exit 3, nothing written anywhere.
    [Theory]
    [InlineData("system", "wf_templates_refuse_system")]
    [InlineData("unknown", "wf_templates_refuse_unknown")]
    [InlineData("suspended", "wf_templates_refuse_suspended")]
    [InlineData("missing", "wf_templates_refuse_missing")]
    public async Task A_refused_tenant_exits_3_and_nothing_is_written(string which, string database)
    {
        await using var world = await World.StartAsync(database);
        string[] arguments = which switch
        {
            "system" => ["--tenant", (await world.ActiveTenantAsync(SystemTenantRules.PlatformSystemTenantId)).ToString(), "--apply"],
            "unknown" => ["--tenant", Guid.NewGuid().ToString(), "--apply"],
            "suspended" => ["--tenant", (await world.ActiveTenantAsync(status: TenantStatus.Suspended)).ToString(), "--apply"],
            _ => ["--apply"]
        };
        var before = await world.AllCollectionCountsAsync();

        var (exit, _, error) = await world.RunAsync(arguments);

        Assert.Equal(EnsureWorkflowTemplatesCommandLine.Refused, exit);
        Assert.Contains("Nothing was written", error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, await world.AllCollectionCountsAsync()); // EVERY collection of the database, not only the two
        Assert.Equal(0, await world.WorkflowDocumentCountAsync());
    }

    // (d) two tenants, one refused: the good one is not written either.
    [Fact]
    public async Task One_refused_tenant_out_of_two_means_no_tenant_is_written()
    {
        await using var world = await World.StartAsync("wf_templates_refuse_one_of_two");
        var good = await world.ActiveTenantAsync();
        var suspended = await world.ActiveTenantAsync(status: TenantStatus.Suspended);
        var before = await world.AllCollectionCountsAsync();

        var (exit, _, _) = await world.RunAsync("--tenant", good.ToString(), "--tenant", suspended.ToString(), "--apply");

        Assert.Equal(EnsureWorkflowTemplatesCommandLine.Refused, exit);
        Assert.Equal(before, await world.AllCollectionCountsAsync());
        Assert.Equal(0, await world.WorkflowDocumentCountAsync());
    }

    // (e) without --apply (the default): nothing is written, the list says what would be, exit 0.
    [Fact]
    public async Task Without_apply_it_is_a_dry_run_that_lists_and_writes_nothing()
    {
        await using var world = await World.StartAsync("wf_templates_dry_run");
        var tenant = await world.ActiveTenantAsync();

        var (exit, output, error) = await world.RunAsync("--tenant", tenant.ToString());

        Assert.True(exit == EnsureWorkflowTemplatesCommandLine.Done, error);
        Assert.Equal(7, CountOf(output, EnsureWorkflowTemplatesCommandLine.WouldCreateAndPublish));
        Assert.Contains("DRY RUN", output);
        Assert.Equal(0, await world.WorkflowDocumentCountAsync());
    }

    // (h) THE PUBLISH RACE: a tenant publishes its own version of the definition right before the command's publish reaches
    // the handler's read (after the command's own reads). The tenant's publication stands: its version stays active, the
    // catalog's is never activated, and the command reports drift.
    [Fact]
    public async Task A_publish_that_lands_between_the_decision_and_the_handler_is_never_overwritten()
    {
        var race = new RacingPublish();
        await using var world = await World.StartAsync("wf_templates_race", services =>
            services.Replace(ServiceDescriptor.Scoped<IWorkflowTemplateRepository>(provider => new RacingTemplateRepository(
                ActivatorUtilities.CreateInstance<WorkflowTemplateRepository>(provider),
                provider.GetRequiredService<IWorkflowTemplateVersionRepository>(),
                race))));
        var tenant = await world.ActiveTenantAsync();
        var code = Catalog.Templates[4].TemplateCode;
        var draft = await world.SeedTemplateAsync(tenant, code, WorkflowTemplateStatus.Draft);
        race.Arm(draft.Id, DifferentDefinition);

        var (exit, output, _) = await world.RunAsync("--tenant", tenant.ToString(), "--apply");

        Assert.True(race.Fired, "the rival publish never ran: the race was not staged");
        Assert.Equal(EnsureWorkflowTemplatesCommandLine.NeedsAttention, exit);
        Assert.Contains($"{code,-28} {EnsureWorkflowTemplatesCommandLine.Drift}", output);
        var (template, versions) = await world.DefinitionAsync(tenant, code);
        Assert.Equal(race.RivalVersionId, template!.ActivePublishedVersionId); // the tenant's publication stands
        var only = Assert.Single(versions); // the catalog's version was never written (refused before the write)
        Assert.Equal(race.RivalVersionId, only.Id);
    }

    // v1.1 (3) two valid tenants: each tenant's seven definitions and versions carry THAT tenant's id; no cross write.
    [Fact]
    public async Task Two_tenants_each_get_their_own_seven_and_nothing_crosses()
    {
        await using var world = await World.StartAsync("wf_templates_two_tenants");
        var first = await world.ActiveTenantAsync();
        var second = await world.ActiveTenantAsync();

        var (exit, output, error) = await world.RunAsync("--tenant", first.ToString(), "--tenant", second.ToString(), "--apply");

        Assert.True(exit == EnsureWorkflowTemplatesCommandLine.Done, error);
        Assert.Equal(14, CountOf(output, EnsureWorkflowTemplatesCommandLine.CreatedAndPublished));
        foreach (var tenant in new[] { first, second })
        {
            Assert.Equal(7L, await world.CountForTenantAsync(PlatformCollections.WorkflowTemplates, tenant));
            Assert.Equal(7L, await world.CountForTenantAsync(PlatformCollections.WorkflowTemplateVersions, tenant));
            foreach (var entry in Catalog.Templates)
            {
                var (template, versions) = await world.DefinitionAsync(tenant, entry.TemplateCode);
                Assert.Equal(tenant, template!.TenantId);
                Assert.All(versions, version => Assert.Equal(tenant, version.TenantId));
                Assert.All(versions, version => Assert.Equal(template.Id, version.TemplateId));
            }
        }

        Assert.Equal(28L, await world.WorkflowDocumentCountAsync()); // 2 × (7 definitions + 7 versions), nothing else
    }

    // v1.1 (1) a publish that FAILS after the pointer was written (here: the SLA step after UpdateAsync) is never reported
    // as published: the run stops with exit 4, naming the template, the command and its status.
    [Fact]
    public async Task A_publish_that_fails_after_writing_stops_the_run_with_exit_4_and_is_not_reported_as_published()
    {
        var failure = new OneShotFailure();
        await using var world = await World.StartAsync("wf_templates_publish_failure", services =>
            services.Replace(ServiceDescriptor.Scoped<ISlaEscalationRuleRepository>(provider => new FailingSlaRules(
                ActivatorUtilities.CreateInstance<SlaEscalationRuleRepository>(provider), failure))));
        var tenant = await world.ActiveTenantAsync();
        var code = Catalog.Templates[0].TemplateCode;
        failure.Arm();

        var (exit, output, error) = await world.RunAsync("--tenant", tenant.ToString(), "--apply");

        Assert.True(failure.Fired, "the publish failure was not staged");
        Assert.Equal(EnsureWorkflowTemplatesCommandLine.PartWay, exit);
        Assert.DoesNotContain($"{code,-28} {EnsureWorkflowTemplatesCommandLine.AlreadyPublished}", output);
        Assert.DoesNotContain($"{code,-28} {EnsureWorkflowTemplatesCommandLine.CreatedAndPublished}", output);
        Assert.Contains($"{code}: publish failed (IOException)", error); // the template, the command, the exception's type
        Assert.Contains(tenant.ToString("D"), error);
    }

    // v1.1 (1) the command's OWN unfinished publish (its version written, the pointer not) is not "needs-owner": the next
    // --apply publishes again and completes; the leftover stays an inactive version.
    [Fact]
    public async Task The_commands_own_leftover_version_is_published_again_not_reported_as_needs_owner()
    {
        var failure = new OneShotFailure();
        await using var world = await World.StartAsync("wf_templates_own_leftover", services =>
            services.Replace(ServiceDescriptor.Scoped<IWorkflowTemplateRepository>(provider => new FailingPointerUpdate(
                ActivatorUtilities.CreateInstance<WorkflowTemplateRepository>(provider), failure))));
        var tenant = await world.ActiveTenantAsync();
        var code = Catalog.Templates[5].TemplateCode;
        failure.Arm();

        var (firstExit, _, firstError) = await world.RunAsync("--tenant", tenant.ToString(), "--apply");
        Assert.True(failure.Fired, "the pointer failure was not staged");
        Assert.Equal(EnsureWorkflowTemplatesCommandLine.PartWay, firstExit);
        var (leftover, leftoverVersions) = await world.DefinitionAsync(tenant, code);
        Assert.Equal(WorkflowTemplateStatus.Draft, leftover!.Status); // the command's own version, the pointer never written
        Assert.Equal("system", Assert.Single(leftoverVersions).PublishedBy);
        Assert.Contains(code, firstError);

        var (exit, output, error) = await world.RunAsync("--tenant", tenant.ToString(), "--apply");

        Assert.True(exit == EnsureWorkflowTemplatesCommandLine.Done, error);
        Assert.Contains($"{code,-28} {EnsureWorkflowTemplatesCommandLine.Published}{EnsureWorkflowTemplatesCommandLine.OwnLeftoverSuffix}", output);
        Assert.DoesNotContain(EnsureWorkflowTemplatesCommandLine.NeedsOwner, output);
        var (template, versions) = await world.DefinitionAsync(tenant, code);
        Assert.Equal(WorkflowTemplateStatus.Published, template!.Status);
        Assert.Equal(2, versions.Count);
        Assert.Equal(versions.Single(v => v.VersionNumber == 2).Id, template.ActivePublishedVersionId);
    }

    // v1.1 (2) a failure that is not a race is not retried: the run stops with exit 4, naming the template and the command —
    // a refused Response with its status and REASON CODE (here the publish of a signature step whose meaning does not exist:
    // 400 ESIGN_MEANING_NOT_ALLOWED), a thrown one by its type (here the create validation of an invalid code).
    [Theory]
    [InlineData("publish-refused", "wf_templates_refused_publish")]
    [InlineData("create-throws", "wf_templates_refused_create")]
    public async Task A_non_conflict_failure_stops_the_run_with_exit_4_and_names_what_refused(string which, string database)
    {
        await using var world = await World.StartAsync(database);
        var tenant = await world.ActiveTenantAsync();
        var (code, step) = which == "publish-refused"
            ? ("SIGNED-STEP", """{"code":"approve","type":"approval","requirements":{"signatureRequired":true,"signatureMeaning":"no-such-meaning"}}""")
            : ("NOT A VALID CODE", """{"code":"approve","type":"approval"}""");
        var catalog = Path.Combine(Path.GetTempPath(), $"wf-templates-{which}-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(catalog,
            """{"catalogVersion":1,"templates":[{"templateCode":""" + "\"" + code + "\""
            + ""","name":"Refused","schemaVersion":"1.0","expressionVersion":"1.0","definition":{"stages":[{"code":"review","steps":["""
            + step + "]}]}}]}");
        try
        {
            var (exit, output, error) = await world.RunWithCatalogAsync(catalog, "--tenant", tenant.ToString(), "--apply");

            Assert.Equal(EnsureWorkflowTemplatesCommandLine.PartWay, exit);
            Assert.DoesNotContain(EnsureWorkflowTemplatesCommandLine.AlreadyPublished, output);
            if (which == "publish-refused")
            {
                Assert.Contains($"{code}: publish answered 400 ESIGN_MEANING_NOT_ALLOWED", error);
                Assert.Equal(1L, await world.CountForTenantAsync(PlatformCollections.WorkflowTemplates, tenant)); // created, not published
                Assert.Equal(0L, await world.CountForTenantAsync(PlatformCollections.WorkflowTemplateVersions, tenant));
            }
            else
            {
                Assert.Contains($"{code}: create failed (ValidationException)", error);
                Assert.Equal(0, await world.WorkflowDocumentCountAsync());
            }
        }
        finally
        {
            File.Delete(catalog);
        }
    }

    private sealed class OneShotFailure
    {
        private int _armed;
        public bool Fired { get; private set; }
        public void Arm() => _armed = 1;

        public void ThrowOnce()
        {
            if (Interlocked.Exchange(ref _armed, 0) == 1)
            {
                Fired = true;
                throw new IOException("staged failure");
            }
        }
    }

    private sealed class FailingSlaRules(ISlaEscalationRuleRepository inner, OneShotFailure failure) : ISlaEscalationRuleRepository
    {
        public Task<SlaEscalationRule> CreateAsync(SlaEscalationRule rule, CancellationToken ct = default) => inner.CreateAsync(rule, ct);
        public Task<SlaEscalationRule?> GetByIdAsync(Guid id, CancellationToken ct = default) => inner.GetByIdAsync(id, ct);
        public Task<IReadOnlyList<SlaEscalationRule>> ListActiveByTemplateIdAsync(Guid templateId, CancellationToken ct = default) => inner.ListActiveByTemplateIdAsync(templateId, ct);
        public Task<IReadOnlyList<SlaEscalationRule>> ListActiveAsync(CancellationToken ct = default) => inner.ListActiveAsync(ct);
        public Task<SlaEscalationRule?> FindForStepAsync(Guid templateId, string stageCode, string stepCode, CancellationToken ct = default) =>
            inner.FindForStepAsync(templateId, stageCode, stepCode, ct);

        public Task DeactivateRulesForTemplateAsync(Guid templateId, CancellationToken ct = default)
        {
            failure.ThrowOnce(); // the publish handler's step AFTER the pointer update
            return inner.DeactivateRulesForTemplateAsync(templateId, ct);
        }
    }

    private sealed class FailingPointerUpdate(IWorkflowTemplateRepository inner, OneShotFailure failure) : IWorkflowTemplateRepository
    {
        public Task<WorkflowTemplate> CreateAsync(WorkflowTemplate template, CancellationToken ct = default) => inner.CreateAsync(template, ct);
        public Task<WorkflowTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default) => inner.GetByIdAsync(id, ct);
        public Task<WorkflowTemplate?> GetByTemplateCodeAsync(string templateCode, CancellationToken ct = default) => inner.GetByTemplateCodeAsync(templateCode, ct);
        public Task<IReadOnlyList<WorkflowTemplate>> GetAllForTenantAsync(CancellationToken ct = default) => inner.GetAllForTenantAsync(ct);

        public Task<bool> UpdateAsync(WorkflowTemplate template, int expectedVersion, CancellationToken ct = default)
        {
            if (template.TemplateCode == Catalog.Templates[5].TemplateCode)
            {
                failure.ThrowOnce(); // after the publish handler wrote the version, before the pointer
            }

            return inner.UpdateAsync(template, expectedVersion, ct);
        }
    }

    private static int CountOf(string text, string outcome) =>
        text.Split('\n').Count(line => line.TrimEnd().EndsWith(" " + outcome, StringComparison.Ordinal));

    /// <summary>The rival publish, staged once, right before the publish handler reads the definition.</summary>
    private sealed class RacingPublish
    {
        private Guid _templateId;
        private string _definition = string.Empty;
        public bool Fired { get; private set; }
        public Guid RivalVersionId { get; private set; }

        public void Arm(Guid templateId, string definition)
        {
            _templateId = templateId;
            _definition = definition;
        }

        public async Task FireOnceAsync(Guid templateId, IWorkflowTemplateRepository inner, IWorkflowTemplateVersionRepository versions)
        {
            if (Fired || templateId != _templateId)
            {
                return;
            }

            Fired = true;
            var template = (await inner.GetByIdAsync(templateId))!;
            var version = await versions.CreateAsync(new WorkflowTemplateVersion
            {
                TenantId = template.TenantId,
                TemplateId = template.Id,
                VersionNumber = 1,
                DefinitionJson = _definition,
                SchemaVersion = "1.0",
                ExpressionVersion = "1.0",
                Status = WorkflowTemplateVersionStatus.Published,
                IsImmutable = true,
                PublishedAt = DateTime.UtcNow,
                PublishedBy = "tenant-designer"
            });
            RivalVersionId = version.Id;
            template.Status = WorkflowTemplateStatus.Published;
            template.ActivePublishedVersionId = version.Id;
            template.CurrentVersionId = version.Id;
            Assert.True(await inner.UpdateAsync(template, template.Version));
        }
    }

    private sealed class RacingTemplateRepository(
        IWorkflowTemplateRepository inner, IWorkflowTemplateVersionRepository versions, RacingPublish race) : IWorkflowTemplateRepository
    {
        public Task<WorkflowTemplate> CreateAsync(WorkflowTemplate template, CancellationToken ct = default) => inner.CreateAsync(template, ct);

        public async Task<WorkflowTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            await race.FireOnceAsync(id, inner, versions); // the publish handler's read: the rival lands just before it
            return await inner.GetByIdAsync(id, ct);
        }

        public Task<WorkflowTemplate?> GetByTemplateCodeAsync(string templateCode, CancellationToken ct = default) =>
            inner.GetByTemplateCodeAsync(templateCode, ct);

        public Task<IReadOnlyList<WorkflowTemplate>> GetAllForTenantAsync(CancellationToken ct = default) => inner.GetAllForTenantAsync(ct);

        public Task<bool> UpdateAsync(WorkflowTemplate template, int expectedVersion, CancellationToken ct = default) =>
            inner.UpdateAsync(template, expectedVersion, ct);
    }

    /// <summary>One test's database and the operator wiring over it.</summary>
    private sealed class World : IAsyncDisposable
    {
        private readonly IConfiguration _configuration;
        private readonly Action<IServiceCollection>? _afterRegistrations;
        private readonly ServiceProvider _provider;

        private World(IMongoDatabase database, IConfiguration configuration, Action<IServiceCollection>? afterRegistrations)
        {
            Database = database;
            _configuration = configuration;
            _afterRegistrations = afterRegistrations;
            _provider = Build();
        }

        public IMongoDatabase Database { get; }

        public static async Task<World> StartAsync(string databaseName, Action<IServiceCollection>? afterRegistrations = null)
        {
            var connection = await PlatformTestMongo.ConnectionStringAsync();
            var client = new MongoClient(connection);
            await client.DropDatabaseAsync(databaseName);
            static string Throwaway() => Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
            var configuration = new ConfigurationBuilder()
                .AddConfiguration(WorkflowOrphanRecoveryOperationalMode.ReadConfiguration(OperatorEnvironment, AppContext.BaseDirectory))
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["MongoDbSettings:ConnectionString"] = connection,
                    ["MongoDbSettings:DatabaseName"] = databaseName,
                    ["JwtSettings:Secret"] = Throwaway(),
                    ["AuthService:InternalApiKey"] = Throwaway(),
                    ["ModuleRegistrationCredentials:Mdm:ActiveSecret"] = Throwaway()
                })
                .Build();
            return new World(client.GetDatabase(databaseName), configuration, afterRegistrations);
        }

        private ServiceProvider Build() =>
            WorkflowTemplatesOperationalMode.BuildServices(_configuration, OperatorEnvironment, AppContext.BaseDirectory, _afterRegistrations);

        public Task<(int Exit, string Output, string Error)> RunAsync(params string[] arguments) => RunWithCatalogAsync(CatalogPath, arguments);

        public async Task<(int Exit, string Output, string Error)> RunWithCatalogAsync(string catalogPath, params string[] arguments)
        {
            using var output = new StringWriter();
            using var error = new StringWriter();
            var exit = await EnsureWorkflowTemplatesCommandLine.RunAsync(
                [EnsureWorkflowTemplatesCommandLine.Argument, .. arguments], Build, catalogPath, output, error);
            return (exit, output.ToString(), error.ToString());
        }

        /// <summary>Every collection of the database with its document count.</summary>
        public async Task<IReadOnlyDictionary<string, long>> AllCollectionCountsAsync()
        {
            var counts = new SortedDictionary<string, long>(StringComparer.Ordinal);
            foreach (var name in await (await Database.ListCollectionNamesAsync()).ToListAsync())
            {
                counts[name] = await Database.GetCollection<BsonDocument>(name).CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);
            }

            return counts;
        }

        public async Task<long> CountForTenantAsync(string collection, Guid tenant) =>
            await Database.GetCollection<BsonDocument>(collection).CountDocumentsAsync(
                Builders<BsonDocument>.Filter.Eq("TenantId", new BsonBinaryData(tenant, GuidRepresentation.Standard)));

        public async Task<Guid> ActiveTenantAsync(Guid? id = null, TenantStatus status = TenantStatus.Active)
        {
            var tenantId = id ?? Guid.NewGuid();
            await using var scope = _provider.CreateAsyncScope();
            var code = "WFT" + tenantId.ToString("N")[..8].ToUpperInvariant();
            await scope.ServiceProvider.GetRequiredService<ITenantRegistryRepository>().CreateAsync(new Tenant
            {
                Id = tenantId,
                Code = code,
                Slug = code.ToLowerInvariant(),
                Name = code,
                DisplayName = code,
                Domain = code.ToLowerInvariant() + ".test",
                Status = status
            });
            return tenantId;
        }

        public async Task<WorkflowTemplate> SeedTemplateAsync(Guid tenant, string code, WorkflowTemplateStatus status)
        {
            await using var scope = _provider.CreateAsyncScope();
            using var tenantScope = TenantScope.Begin(scope.ServiceProvider.GetRequiredService<ITenantContext>(), tenant);
            return await scope.ServiceProvider.GetRequiredService<IWorkflowTemplateRepository>().CreateAsync(new WorkflowTemplate
            {
                TenantId = tenant,
                TemplateCode = code,
                Name = "Designed by the tenant",
                Status = status
            });
        }

        public async Task SeedVersionAsync(Guid tenant, Guid templateId, string definition, bool activate)
        {
            await using var scope = _provider.CreateAsyncScope();
            using var tenantScope = TenantScope.Begin(scope.ServiceProvider.GetRequiredService<ITenantContext>(), tenant);
            var templates = scope.ServiceProvider.GetRequiredService<IWorkflowTemplateRepository>();
            var version = await scope.ServiceProvider.GetRequiredService<IWorkflowTemplateVersionRepository>().CreateAsync(
                new WorkflowTemplateVersion
                {
                    TenantId = tenant,
                    TemplateId = templateId,
                    VersionNumber = 1,
                    DefinitionJson = definition,
                    SchemaVersion = "1.0",
                    ExpressionVersion = "1.0",
                    Status = WorkflowTemplateVersionStatus.Published,
                    IsImmutable = true,
                    PublishedAt = DateTime.UtcNow,
                    PublishedBy = "tenant-designer"
                });
            if (activate)
            {
                var template = (await templates.GetByIdAsync(templateId))!;
                template.Status = WorkflowTemplateStatus.Published;
                template.ActivePublishedVersionId = version.Id;
                template.CurrentVersionId = version.Id;
                Assert.True(await templates.UpdateAsync(template, template.Version));
            }
        }

        public async Task<(WorkflowTemplate? Template, IReadOnlyList<WorkflowTemplateVersion> Versions)> DefinitionAsync(Guid tenant, string code)
        {
            await using var scope = _provider.CreateAsyncScope();
            using var tenantScope = TenantScope.Begin(scope.ServiceProvider.GetRequiredService<ITenantContext>(), tenant);
            var template = await scope.ServiceProvider.GetRequiredService<IWorkflowTemplateRepository>().GetByTemplateCodeAsync(code);
            var versions = template is null
                ? []
                : await scope.ServiceProvider.GetRequiredService<IWorkflowTemplateVersionRepository>().ListByTemplateIdAsync(template.Id);
            return (template, versions);
        }

        public async Task<long> TemplateCountAsync(Guid tenant, string code) =>
            await Database.GetCollection<BsonDocument>(PlatformCollections.WorkflowTemplates).CountDocumentsAsync(
                Builders<BsonDocument>.Filter.Eq("TemplateCode", code)
                & Builders<BsonDocument>.Filter.Eq("TenantId", new BsonBinaryData(tenant, GuidRepresentation.Standard)));

        /// <summary>The definition and all its versions, as stored (byte-for-byte comparison).</summary>
        public async Task<string> RawDefinitionAsync(Guid tenant, string code)
        {
            var template = await Database.GetCollection<BsonDocument>(PlatformCollections.WorkflowTemplates)
                .Find(Builders<BsonDocument>.Filter.Eq("TemplateCode", code)).SingleAsync();
            var versions = await Database.GetCollection<BsonDocument>(PlatformCollections.WorkflowTemplateVersions)
                .Find(Builders<BsonDocument>.Filter.Eq("TemplateId", template["_id"])).ToListAsync();
            return template.ToJson() + "\n" + string.Join("\n", versions.Select(v => v.ToJson()));
        }

        public async Task<string> SnapshotAsync()
        {
            var templates = await Database.GetCollection<BsonDocument>(PlatformCollections.WorkflowTemplates)
                .Find(FilterDefinition<BsonDocument>.Empty).Sort(Builders<BsonDocument>.Sort.Ascending("_id")).ToListAsync();
            var versions = await Database.GetCollection<BsonDocument>(PlatformCollections.WorkflowTemplateVersions)
                .Find(FilterDefinition<BsonDocument>.Empty).Sort(Builders<BsonDocument>.Sort.Ascending("_id")).ToListAsync();
            return string.Join("\n", templates.Concat(versions).Select(document => document.ToJson()));
        }

        public async Task<long> WorkflowDocumentCountAsync() =>
            await Database.GetCollection<BsonDocument>(PlatformCollections.WorkflowTemplates).CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)
            + await Database.GetCollection<BsonDocument>(PlatformCollections.WorkflowTemplateVersions).CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);

        public async ValueTask DisposeAsync() => await _provider.DisposeAsync();
    }
}

/// <summary>
/// WP-PLT-WORKFLOW-TEMPLATES-01 (f) — the catalog, MDM's trusted flow codes (every <c>*TemplateCode</c> of its workflow
/// sections, MDM-COMP-PROPOSAL excluded) and Platform's TrustedWorkflowStartAuthorization codes are the SAME set: a code
/// added to one and forgotten in another is red.
/// </summary>
public sealed class WorkflowTemplateCatalogCodesTests
{
    [Fact]
    public void Catalog_codes_equal_MDM_trusted_flow_codes_and_Platform_trusted_start_codes()
    {
        var catalog = WorkflowTemplateCatalog.Load(Path.Combine(AppContext.BaseDirectory, WorkflowTemplateCatalog.RelativePath))
            .Templates.Select(t => t.TemplateCode).Order(StringComparer.Ordinal).ToArray();

        var platform = JsonNode.Parse(File.ReadAllText(RepoFile("services", "Diten.Platform", "src", "Diten.Platform.API", "appsettings.Development.json")))!
            ["TrustedWorkflowStartAuthorization"]!["Entries"]!.AsArray()
            .Select(entry => entry!["TemplateCode"]!.GetValue<string>())
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

        var mdmRoot = JsonNode.Parse(File.ReadAllText(RepoFile("services", "Diten.MdmService", "src", "Diten.MdmService.Api", "appsettings.Development.json")))!.AsObject();
        var mdm = mdmRoot
            .Where(section => section.Key.EndsWith("Workflow", StringComparison.Ordinal) && section.Value is JsonObject)
            .SelectMany(section => section.Value!.AsObject()
                .Where(pair => pair.Key.EndsWith("TemplateCode", StringComparison.Ordinal))
                .Select(pair => pair.Value!.GetValue<string>()))
            .Where(code => code != "MDM-COMP-PROPOSAL")
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(7, catalog.Length);
        Assert.Equal(catalog, mdm);
        Assert.Equal(catalog, platform);
    }

    private static string RepoFile(params string[] segments)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "AGENTS.md")))
        {
            current = current.Parent;
        }

        return Path.Combine([current?.FullName ?? throw new InvalidOperationException("Checkout required"), .. segments]);
    }
}
