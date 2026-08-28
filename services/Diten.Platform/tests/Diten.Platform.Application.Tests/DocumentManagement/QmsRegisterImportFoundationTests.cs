using System.Text;
using System.Text.RegularExpressions;
using Diten.Platform.Application.Features.DocumentManagementQmsBaseline;
using Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Commands;
using Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using Xunit;

namespace Diten.Platform.Application.Tests.DocumentManagement;

/// <summary>
/// QMS register import extension — governance identity pending. Verifies the GMG-QMS-LOG-0007 v0.8 CSV / flat-JSON
/// package parses into a governance-complete DRAFT baseline with a stable, rename-safe folder identity, without
/// disturbing the legacy path-hash behaviour.
/// </summary>
public sealed class QmsRegisterImportFoundationTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private const string RegisterKey = "GMG-QMS-LOG-0007";
    private const string RegisterVersion = "0.8";
    private const string Correlation = "fu06-corr-001";

    // Parent pack CanonicalId contract.
    private static readonly Regex CanonicalIdFormat = new("^CAN-[A-Z0-9]{2,10}-[A-Z0-9]{2,16}-[0-9]{3,6}$");

    private static QmsFolderTreeValidator Validator() => new();

    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "DocumentManagement", name);

    // ── FU07: CSV package parses to 2,175 governance-complete definitions ───────────────────────────

    [Fact]
    public async Task Csv_package_parses_all_2175_folders_into_a_valid_ordered_tree()
    {
        var bytes = await File.ReadAllBytesAsync(FixturePath("00_all_folders_2175.csv"));
        var rows = await new CsvQmsFolderImportParser().ParseAsync(bytes);

        Assert.Equal(2175, rows.Count);

        var plan = Validator().BuildPlan(rows, TenantA, RegisterKey);

        Assert.True(plan.Summary.IsValid);
        Assert.Equal(2175, plan.Summary.ImportedDefinitionsCount);
        Assert.Equal(0, plan.Summary.SkippedRows);
        Assert.Empty(plan.Summary.InvalidHierarchyFindings);
        Assert.Empty(plan.Summary.DuplicatePathConflicts);

        // Exactly one root and it is the register root ENT-ROOT / GMG-Group-Enterprise.
        var roots = plan.Definitions.Where(d => d.ParentCanonicalId is null).ToList();
        var root = Assert.Single(roots);
        Assert.Equal("GMG-Group-Enterprise", root.FullPath);
        Assert.Equal("ENT-ROOT", root.RegisterFolderId);

        // Wave counts from the register.
        Assert.Equal(24, plan.Definitions.Count(d => (d.ProvisioningWave ?? "").StartsWith("Wave 1", StringComparison.Ordinal)));
        Assert.Equal(412, plan.Definitions.Count(d => (d.ProvisioningWave ?? "").StartsWith("Wave 2", StringComparison.Ordinal)));
        Assert.Equal(1425, plan.Definitions.Count(d => (d.ProvisioningWave ?? "").StartsWith("Wave 3", StringComparison.Ordinal)));
        Assert.Equal(314, plan.Definitions.Count(d => (d.ProvisioningWave ?? "").StartsWith("Wave 4", StringComparison.Ordinal)));

        // Every CanonicalId honours the parent pack format.
        Assert.All(plan.Definitions, d => Assert.Matches(CanonicalIdFormat, d.CanonicalId));
    }

    [Fact]
    public async Task Flat_json_package_parses_2175_folders_and_reads_register_metadata()
    {
        var bytes = await File.ReadAllBytesAsync(FixturePath("00_folder_list_flat.json"));
        var parser = new FlatJsonQmsFolderImportParser();

        var (metadata, rows) = parser.ParseWithMetadata(bytes);

        Assert.Equal(2175, rows.Count);
        Assert.NotNull(metadata);
        Assert.Equal("GMG-QMS-LOG-0007", metadata!.Register);
        Assert.Equal("0.8", metadata.Version);

        var plan = Validator().BuildPlan(rows, TenantA, RegisterKey);
        Assert.True(plan.Summary.IsValid);
        Assert.Equal(2175, plan.Definitions.Count);
    }

    // ── FU06: governance metadata mapping ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Governance_columns_are_mapped_onto_each_definition()
    {
        // Minimal 2-row register CSV with a full governance payload on the child row.
        var csv = string.Join('\n',
            "folder_id,parent_folder_id,folder_name,full_path,level,department_domain,folder_type,purpose,example_documents,owning_departments,controlled_by_gqms,source_of_truth,owner_function,access_profile,retention_class,change_control_required,gqms_scope_link,legacy_code,provisioning_wave,provisioning_order",
            "ENT-ROOT,,GMG-Group-Enterprise,GMG-Group-Enterprise,0,Root,Enterprise Root,Root purpose,,,Partly,Taxonomy,CEO / IT,Enterprise-Restricted,Active,Yes,GMG-QMS-LOG-0005,,Wave 1 – Enterprise skeleton,1",
            "ENT-01,ENT-ROOT,01_Quality_GQMS,GMG-Group-Enterprise/01_Quality_GQMS,1,Quality / GQMS,Domain Root,Quality domain,\"SOPs, manuals\",QA Documentation,Yes,Enterprise Folder Taxonomy,GQD / QA Documentation,GQMS-Controlled,Active,Yes,GMG-QMS-LOG-0005,04.04,Wave 1 – Enterprise skeleton,3");

        var rows = await new CsvQmsFolderImportParser().ParseAsync(Encoding.UTF8.GetBytes(csv));
        var plan = Validator().BuildPlan(rows, TenantA, RegisterKey);

        var quality = plan.Definitions.Single(d => d.Name == "01_Quality_GQMS");
        Assert.Equal("ENT-01", quality.RegisterFolderId);
        Assert.Equal("ENT-ROOT", quality.RegisterParentFolderId);
        Assert.Equal("Quality / GQMS", quality.DepartmentDomain);
        Assert.Equal("Domain Root", quality.FolderType);
        Assert.Equal("SOPs, manuals", quality.ExampleDocuments);
        Assert.Equal("QA Documentation", quality.OwningDepartments);
        Assert.Equal("Yes", quality.ControlledByGqms);
        Assert.Equal("Enterprise Folder Taxonomy", quality.SourceOfTruth);
        Assert.Equal("GQD / QA Documentation", quality.OwnerFunction);
        Assert.Equal("GQMS-Controlled", quality.AccessProfile);
        Assert.Equal("Active", quality.RetentionClass);
        Assert.Equal("Yes", quality.ChangeControlRequired);
        Assert.Equal("GMG-QMS-LOG-0005", quality.GqmsScopeLink);
        Assert.Equal("04.04", quality.LegacyCode);
        Assert.Equal("Quality domain", quality.PurposeScope); // purpose → PurposeScope
        Assert.Equal(3, quality.ProvisioningOrder);
    }

    // ── FU06: stable, rename-safe identity ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Same_folder_id_yields_same_identity_even_when_name_and_path_change()
    {
        var before = await ParseChildCanonical("ENT-01", "01_Quality_GQMS");
        var afterRename = await ParseChildCanonical("ENT-01", "01_Quality_System"); // renamed folder, same folder_id

        Assert.Equal(before, afterRename);
        Assert.Matches(CanonicalIdFormat, before);
    }

    [Fact]
    public async Task Legacy_rows_without_folder_id_keep_path_hash_fallback()
    {
        // No folder_id column at all → legacy path-hash identity, which DOES change when the path changes.
        var a = await ParseLegacyChildCanonical("Root/Quality");
        var b = await ParseLegacyChildCanonical("Root/Quality System");

        Assert.NotEqual(a, b);
        Assert.Matches(CanonicalIdFormat, a);
    }

    [Fact]
    public async Task Legacy_import_leaves_all_register_governance_fields_null()
    {
        var csv = string.Join('\n',
            "full_path",
            "Root",
            "Root/Child");

        var rows = await new CsvQmsFolderImportParser().ParseAsync(Encoding.UTF8.GetBytes(csv));
        var plan = Validator().BuildPlan(rows, TenantA, RegisterKey);

        Assert.All(plan.Definitions, d =>
        {
            Assert.Null(d.RegisterFolderId);
            Assert.Null(d.AccessProfile);
            Assert.Null(d.LegacyCode);
            Assert.Null(d.ProvisioningWave);
        });
    }

    // ── FU06: commit produces a DRAFT baseline with governance persisted, nothing published ─────────

    [Fact]
    public async Task Commit_register_csv_creates_draft_baseline_with_governance_and_no_publish()
    {
        var bytes = await File.ReadAllBytesAsync(FixturePath("00_all_folders_2175.csv"));
        var contentBase64 = Convert.ToBase64String(bytes);

        var tenantContext = Resolved(TenantA);
        var baselineRepo = new FakeBaselineReleaseRepository();
        var definitionRepo = new FakeCollectionDefinitionRepository();
        var importService = new QmsBaselineImportService(
            [new CsvQmsFolderImportParser()], Validator(), new DottedOutlineTreeBuilder());
        var handler = new CommitQmsBaselineImportHandler(importService, baselineRepo, definitionRepo, tenantContext);

        var response = await handler.Handle(
            new CommitQmsBaselineImportCommand(
                "00_all_folders_2175.csv", "csv", contentBase64, RegisterKey, RegisterVersion, "v0.8 DRAFT", Correlation),
            CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(201, response.StatusCode);

        var baseline = Assert.Single(baselineRepo.Created);
        Assert.Equal(RegisterKey, baseline.SourceBaselineKey);
        Assert.Equal(RegisterVersion, baseline.BaselineVersion);
        Assert.Equal(BaselineReleaseStatus.Draft, baseline.Status); // never Published/Effective in this FU
        Assert.Null(baseline.PublishedAt);
        Assert.Null(baseline.SnapshotHash);

        Assert.Equal(2175, definitionRepo.Created.Count);
        Assert.All(definitionRepo.Created, d => Assert.Equal(CollectionDefinitionStatus.Active, d.Status));

        var quality = definitionRepo.Created.Single(d => d.RegisterFolderId == "ENT-01");
        Assert.Equal("GQMS-Controlled", quality.AccessProfile);
        Assert.Equal("Domain Root", quality.FolderType);
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────

    private static async Task<string> ParseChildCanonical(string childFolderId, string childName)
    {
        var csv = string.Join('\n',
            "folder_id,parent_folder_id,folder_name,full_path,level,provisioning_order",
            "ENT-ROOT,,GMG-Group-Enterprise,GMG-Group-Enterprise,0,1",
            $"{childFolderId},ENT-ROOT,{childName},GMG-Group-Enterprise/{childName},1,2");

        var rows = await new CsvQmsFolderImportParser().ParseAsync(Encoding.UTF8.GetBytes(csv));
        var plan = Validator().BuildPlan(rows, TenantA, RegisterKey);
        return plan.Definitions.Single(d => d.RegisterFolderId == childFolderId).CanonicalId;
    }

    private static async Task<string> ParseLegacyChildCanonical(string childFullPath)
    {
        var csv = string.Join('\n',
            "full_path",
            "Root",
            childFullPath);

        var rows = await new CsvQmsFolderImportParser().ParseAsync(Encoding.UTF8.GetBytes(csv));
        var plan = Validator().BuildPlan(rows, TenantA, RegisterKey);
        return plan.Definitions.Single(d => d.FullPath == childFullPath).CanonicalId;
    }

    private static TenantContext Resolved(Guid tenantId)
    {
        var ctx = new TenantContext();
        ctx.SetTenant(tenantId);
        return ctx;
    }

    private sealed class FakeBaselineReleaseRepository : IBaselineReleaseRepository
    {
        public List<BaselineRelease> Created { get; } = [];

        public Task<BaselineRelease> CreateAsync(BaselineRelease baseline, CancellationToken ct = default)
        {
            Created.Add(baseline);
            return Task.FromResult(baseline);
        }

        public Task<BaselineRelease?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Created.FirstOrDefault(x => x.Id == id));

        public Task<IReadOnlyList<BaselineRelease>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<BaselineRelease>>(Created);

        public Task<bool> UpdateAsync(BaselineRelease baseline, int expectedVersion, CancellationToken ct = default)
        {
            baseline.Version = expectedVersion + 1;
            return Task.FromResult(true);
        }
    }

    private sealed class FakeCollectionDefinitionRepository : ICollectionDefinitionRepository
    {
        public List<CollectionDefinition> Created { get; } = [];

        public Task<CollectionDefinition> CreateAsync(CollectionDefinition definition, CancellationToken ct = default)
        {
            Created.Add(definition);
            return Task.FromResult(definition);
        }

        public Task CreateManyAsync(IReadOnlyList<CollectionDefinition> definitions, CancellationToken ct = default)
        {
            Created.AddRange(definitions);
            return Task.CompletedTask;
        }

        public Task<bool> UpdateAsync(CollectionDefinition definition, int expectedVersion, CancellationToken ct = default) =>
            Task.FromResult(true);

        public Task UpdateManyAsync(IReadOnlyList<CollectionDefinition> definitions, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<bool> SoftDeleteAsync(CollectionDefinition definition, int expectedVersion, CancellationToken ct = default) =>
            Task.FromResult(true);

        public Task<IReadOnlyList<CollectionDefinition>> GetByBaselineAsync(Guid baselineReleaseId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CollectionDefinition>>(Created.Where(x => x.BaselineReleaseId == baselineReleaseId).ToList());

        public Task<CollectionDefinition?> GetByCanonicalIdAsync(Guid baselineReleaseId, string canonicalId, CancellationToken ct = default) =>
            Task.FromResult(Created.FirstOrDefault(x => x.BaselineReleaseId == baselineReleaseId && x.CanonicalId == canonicalId));
    }
}
