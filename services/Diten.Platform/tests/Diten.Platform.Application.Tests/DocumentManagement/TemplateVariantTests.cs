using Diten.Platform.Application.Features.DocumentManagementTemplateVariants;
using Diten.Platform.Application.Features.DocumentManagementTemplateVariants.Services;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using Xunit;

namespace Diten.Platform.Application.Tests.DocumentManagement;

// MOD-0029-FU03 — template variant governance + drift service tests (in-memory fakes, no Mongo).
public sealed class TemplateVariantTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid ScopeId = Guid.Parse("aaaaaaaa-1111-2222-3333-444444444444");
    private static readonly Guid FolderId = Guid.Parse("bbbbbbbb-1111-2222-3333-444444444444");
    private const string Corr = "fu03-corr-1";

    [Fact]
    public async Task Create_variant_from_published_master_initializes_lineage_and_is_in_sync()
    {
        var f = Fixture();
        var (master, _) = SeedPublishedMaster(f, currentVersion: 1);

        var response = await f.Service.CreateAsync(Input(master) with { VariantCode = " comp-sop-001 " }, Corr, CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(201, response.StatusCode);
        var variant = Assert.Single(f.Variants.Items);
        Assert.Equal("COMP-SOP-001", variant.VariantCode);
        Assert.Equal(1, variant.LastRebasedMasterVersionNumber);
        Assert.NotNull(variant.LastRebasedAt);
        Assert.False(variant.HasLocalChanges);
        Assert.Equal(TemplateVariantContentSource.MasterVersion, variant.ContentSource);
        Assert.NotNull(variant.LinkedTemplateDocumentId);
        Assert.Equal("INSYNC", response.Data!.DriftStatus);
        Assert.Equal("MASTER_VERSION", response.Data.ContentSource);
        Assert.True(response.Data.UsesMasterContent);

        var template = Assert.Single(f.TemplateDocuments.Items);
        Assert.Equal(variant.LinkedTemplateDocumentId, template.Id);
        Assert.Equal(master.Id, template.TemplateMasterId);
        Assert.Equal(master.CurrentVersionId, template.TemplateMasterVersionId);
        Assert.Equal(FolderId, template.CollectionInstanceId);
        Assert.Equal(ScopeId, template.CompanyId);
        Assert.Equal("Quality/Manuals", template.CollectionPath);

        var templateVersion = Assert.Single(f.TemplateVersions.Items);
        Assert.Equal(template.Id, templateVersion.TemplateId);
        Assert.Equal(1, templateVersion.VersionNumber);
        Assert.Equal("c1", templateVersion.Checksum);
        Assert.Equal("master.docx", templateVersion.FileRef.FileName);
        Assert.Equal(templateVersion.Id, templateVersion.FileRef.VersionId);
    }

    [Fact]
    public async Task Cannot_create_variant_from_deprecated_master()
    {
        var f = Fixture();
        var (master, _) = SeedPublishedMaster(f, currentVersion: 1);
        master.Status = TemplateMasterStatus.Deprecated;

        var response = await f.Service.CreateAsync(Input(master), Corr, CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Equal(TemplateVariantReasonCodes.MasterInactive, response.ReasonCode);
        Assert.Empty(f.Variants.Items);
    }

    [Fact]
    public async Task Cannot_create_variant_from_archived_master()
    {
        var f = Fixture();
        var (master, _) = SeedPublishedMaster(f, currentVersion: 1);
        master.Status = TemplateMasterStatus.Archived;

        var response = await f.Service.CreateAsync(Input(master), Corr, CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Equal(TemplateVariantReasonCodes.MasterInactive, response.ReasonCode);
        Assert.Empty(f.Variants.Items);
    }

    [Fact]
    public async Task Create_with_master_version_not_belonging_to_master_is_rejected()
    {
        var f = Fixture();
        var (master, _) = SeedPublishedMaster(f, currentVersion: 1);

        var response = await f.Service.CreateAsync(Input(master) with { TemplateMasterVersionId = Guid.NewGuid() }, Corr, CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Equal(TemplateVariantReasonCodes.InvalidMasterVersion, response.ReasonCode);
        Assert.Empty(f.Variants.Items);
    }

    [Fact]
    public async Task Create_with_missing_master_returns_not_found_non_leakage()
    {
        var f = Fixture();

        var response = await f.Service.CreateAsync(
            new CreateTemplateVariantInput(Guid.NewGuid(), Guid.NewGuid(), "X", "X", null, "COMPANY", ScopeId, FolderId, "MASTER_VERSION", null, null, null, null),
            Corr, CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
        Assert.Equal(TemplateVariantReasonCodes.NotFoundNonLeakage, response.ReasonCode);
    }

    [Fact]
    public async Task Create_with_unknown_content_source_is_rejected()
    {
        var f = Fixture();
        var (master, _) = SeedPublishedMaster(f, currentVersion: 1);

        var response = await f.Service.CreateAsync(Input(master) with { ContentSource = "SOMETHING_ELSE" }, Corr, CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Equal(TemplateVariantReasonCodes.InvalidContentSource, response.ReasonCode);
        Assert.Empty(f.Variants.Items);
    }

    [Fact]
    public async Task Create_with_master_content_rejects_local_file()
    {
        var f = Fixture();
        var (master, _) = SeedPublishedMaster(f, currentVersion: 1);

        var response = await f.Service.CreateAsync(Input(master) with { LocalFile = LocalFile() }, Corr, CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Equal(TemplateVariantReasonCodes.LocalFileNotAllowed, response.ReasonCode);
        Assert.Empty(f.Variants.Items);
    }

    [Fact]
    public async Task Create_with_local_upload_requires_file()
    {
        var f = Fixture();
        var (master, _) = SeedPublishedMaster(f, currentVersion: 1);

        var response = await f.Service.CreateAsync(Input(master) with { ContentSource = "LOCAL_UPLOAD" }, Corr, CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Equal(TemplateVariantReasonCodes.LocalFileRequired, response.ReasonCode);
        Assert.Empty(f.Variants.Items);
    }

    [Fact]
    public async Task Create_with_local_upload_uses_uploaded_content_and_is_drifted()
    {
        var f = Fixture();
        var (master, _) = SeedPublishedMaster(f, currentVersion: 1);

        var response = await f.Service.CreateAsync(Input(master) with { ContentSource = "LOCAL_UPLOAD", LocalFile = LocalFile("local.docx", "local-content") }, Corr, CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal("DRIFTED", response.Data!.DriftStatus);
        Assert.Equal("LOCAL_UPLOAD", response.Data.ContentSource);
        Assert.False(response.Data.UsesMasterContent);
        var variant = Assert.Single(f.Variants.Items);
        Assert.True(variant.HasLocalChanges);
        Assert.Equal(TemplateVariantContentSource.LocalUpload, variant.ContentSource);
        var version = Assert.Single(f.TemplateVersions.Items);
        Assert.Equal("local.docx", version.FileRef.FileName);
        Assert.NotEqual("c1", version.Checksum);
        Assert.Single(f.Storage.Stored);
    }

    [Fact]
    public async Task Local_upload_storage_failure_creates_no_metadata_orphan()
    {
        var f = Fixture();
        f.Storage.FailStore = true;
        var (master, _) = SeedPublishedMaster(f, currentVersion: 1);

        var response = await f.Service.CreateAsync(Input(master) with { ContentSource = "LOCAL_UPLOAD", LocalFile = LocalFile() }, Corr, CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(503, response.StatusCode);
        Assert.Empty(f.Variants.Items);
        Assert.Empty(f.TemplateDocuments.Items);
        Assert.Empty(f.TemplateVersions.Items);
    }

    [Fact]
    public async Task Local_upload_metadata_failure_deletes_stored_content_and_soft_deletes_template()
    {
        var f = Fixture();
        f.TemplateDocuments.FailCreate = true;
        var (master, _) = SeedPublishedMaster(f, currentVersion: 1);

        var response = await f.Service.CreateAsync(Input(master) with { ContentSource = "LOCAL_UPLOAD", LocalFile = LocalFile() }, Corr, CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(TemplateVariantReasonCodes.LinkedTemplateCreateFailed, response.ReasonCode);
        Assert.Empty(f.Variants.Items);
        Assert.Empty(f.TemplateVersions.Items);
        Assert.Single(f.Storage.Stored);
        Assert.Single(f.Storage.Deleted);
    }

    [Fact]
    public async Task Duplicate_variant_code_per_tenant_scope_is_rejected()
    {
        var f = Fixture();
        var (master, _) = SeedPublishedMaster(f, currentVersion: 1);
        await f.Service.CreateAsync(Input(master), Corr, CancellationToken.None);

        var response = await f.Service.CreateAsync(Input(master) with { VariantName = "Other" }, Corr, CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Equal(TemplateVariantReasonCodes.DuplicateVariantCode, response.ReasonCode);
        Assert.Single(f.Variants.Items);
        Assert.Single(f.TemplateDocuments.Items);
        Assert.Single(f.TemplateVersions.Items);
    }

    [Fact]
    public async Task Create_with_invalid_target_folder_is_rejected_without_orphan_template()
    {
        var f = Fixture();
        var (master, _) = SeedPublishedMaster(f, currentVersion: 1);

        var response = await f.Service.CreateAsync(Input(master) with { TargetCollectionInstanceId = Guid.NewGuid() }, Corr, CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
        Assert.Equal(TemplateVariantReasonCodes.NotFoundNonLeakage, response.ReasonCode);
        Assert.Empty(f.Variants.Items);
        Assert.Empty(f.TemplateDocuments.Items);
        Assert.Empty(f.TemplateVersions.Items);
    }

    [Fact]
    public async Task Create_without_create_template_permission_returns_403_without_orphan_template()
    {
        var f = Fixture(enforceTargetFolderPermission: true);
        var (master, _) = SeedPublishedMaster(f, currentVersion: 1);

        var response = await f.Service.CreateAsync(Input(master), Corr, CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(403, response.StatusCode);
        Assert.Equal(TemplateVariantReasonCodes.PermissionDenied, response.ReasonCode);
        Assert.Empty(f.Variants.Items);
        Assert.Empty(f.TemplateDocuments.Items);
        Assert.Empty(f.TemplateVersions.Items);
    }

    [Fact]
    public async Task Create_with_missing_master_content_is_rejected_without_orphan_template()
    {
        var f = Fixture();
        var (master, version) = SeedPublishedMaster(f, currentVersion: 1);
        version.FileRef = null!;

        var response = await f.Service.CreateAsync(Input(master), Corr, CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Equal(TemplateVariantReasonCodes.InvalidMasterContent, response.ReasonCode);
        Assert.Empty(f.Variants.Items);
        Assert.Empty(f.TemplateDocuments.Items);
        Assert.Empty(f.TemplateVersions.Items);
    }

    [Fact]
    public void Drift_in_sync_when_versions_match_and_no_local_changes()
    {
        var master = NewMaster(currentVersion: 3, status: TemplateMasterStatus.Published);
        var variant = NewVariant(master, rebasedNumber: 3, hasLocalChanges: false);
        Assert.Equal(TemplateVariantDriftStatus.InSync, TemplateVariantService.ComputeDrift(variant, master));
    }

    [Fact]
    public void Drift_rebase_required_when_master_is_ahead()
    {
        var master = NewMaster(currentVersion: 5, status: TemplateMasterStatus.Published);
        var variant = NewVariant(master, rebasedNumber: 3, hasLocalChanges: false);
        Assert.Equal(TemplateVariantDriftStatus.RebaseRequired, TemplateVariantService.ComputeDrift(variant, master));
    }

    [Fact]
    public void Drift_drifted_wins_over_rebase_required_when_local_changes()
    {
        var master = NewMaster(currentVersion: 5, status: TemplateMasterStatus.Published);
        var variant = NewVariant(master, rebasedNumber: 3, hasLocalChanges: true);
        Assert.Equal(TemplateVariantDriftStatus.Drifted, TemplateVariantService.ComputeDrift(variant, master));
    }

    [Fact]
    public void Drift_blocked_when_master_deprecated()
    {
        var master = NewMaster(currentVersion: 3, status: TemplateMasterStatus.Deprecated);
        var variant = NewVariant(master, rebasedNumber: 3, hasLocalChanges: true);
        Assert.Equal(TemplateVariantDriftStatus.Blocked, TemplateVariantService.ComputeDrift(variant, master));
    }

    [Fact]
    public void Drift_blocked_when_variant_approval_blocked()
    {
        var master = NewMaster(currentVersion: 3, status: TemplateMasterStatus.Published);
        var variant = NewVariant(master, rebasedNumber: 3, hasLocalChanges: false);
        variant.ApprovalStatus = TemplateVariantApprovalStatus.Blocked;
        Assert.Equal(TemplateVariantDriftStatus.Blocked, TemplateVariantService.ComputeDrift(variant, master));
    }

    [Fact]
    public async Task Compare_returns_metadata_placeholder_without_binary_diff()
    {
        var f = Fixture();
        var (master, _) = SeedPublishedMaster(f, currentVersion: 2);
        var created = await f.Service.CreateAsync(Input(master), Corr, CancellationToken.None);

        var response = await f.Service.CompareAsync(created.Data!.Id, Corr, CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(master.Id, response.Data!.TemplateMasterId);
        Assert.Equal(2, response.Data.MasterCurrentVersion);
        Assert.Null(response.Data.ChecksumEqual);
        Assert.Equal("INSYNC", response.Data.DriftStatus);
        Assert.Equal("MASTER_VERSION", response.Data.ContentSource);
        Assert.True(response.Data.ContentLinked);
        Assert.Equal(FolderId, response.Data.CollectionInstanceId);
    }

    [Fact]
    public async Task Rebase_updates_lineage_and_clears_local_changes()
    {
        var f = Fixture();
        var (master, v1) = SeedPublishedMaster(f, currentVersion: 1);
        var created = await f.Service.CreateAsync(Input(master), Corr, CancellationToken.None);

        // The master publishes a new current version; the variant develops local changes → Drifted.
        var v2 = new TemplateMasterVersion { Id = Guid.NewGuid(), TenantId = TenantId, TemplateMasterId = master.Id, VersionNumber = 2, FileRef = Content("c2"), Checksum = "c2" };
        f.MasterVersions.Items.Add(v2);
        master.CurrentVersionId = v2.Id;
        master.CurrentMasterVersion = 2;
        var variant = f.Variants.Items.Single();
        variant.HasLocalChanges = true;

        var beforeDrift = await f.Service.GetDetailAsync(created.Data!.Id, Corr, CancellationToken.None);
        Assert.Equal("DRIFTED", beforeDrift.Data!.DriftStatus);

        var response = await f.Service.RebaseAsync(created.Data!.Id, new RebaseTemplateVariantInput(null), Corr, CancellationToken.None);

        Assert.True(response.IsSuccessful);
        var rebased = f.Variants.Items.Single();
        Assert.Equal(v2.Id, rebased.LastRebasedMasterVersionId);
        Assert.Equal(2, rebased.LastRebasedMasterVersionNumber);
        Assert.NotNull(rebased.LastRebasedAt);
        Assert.False(rebased.HasLocalChanges);
        Assert.Equal("INSYNC", response.Data!.DriftStatus);
        Assert.Equal("fu01@example.test", rebased.UpdatedBy);
        Assert.Equal("c1", f.TemplateVersions.Items.Single().Checksum);
    }

    [Fact]
    public async Task Rebase_does_not_overwrite_local_uploaded_template_content()
    {
        var f = Fixture();
        var (master, _) = SeedPublishedMaster(f, currentVersion: 1);
        var created = await f.Service.CreateAsync(Input(master) with { ContentSource = "LOCAL_UPLOAD", LocalFile = LocalFile("local.docx", "local-content") }, Corr, CancellationToken.None);
        var localChecksum = f.TemplateVersions.Items.Single().Checksum;
        var localObjectKey = f.TemplateVersions.Items.Single().FileRef.ObjectKey;

        var v2 = new TemplateMasterVersion { Id = Guid.NewGuid(), TenantId = TenantId, TemplateMasterId = master.Id, VersionNumber = 2, FileRef = Content("c2"), Checksum = "c2" };
        f.MasterVersions.Items.Add(v2);
        master.CurrentVersionId = v2.Id;
        master.CurrentMasterVersion = 2;

        var response = await f.Service.RebaseAsync(created.Data!.Id, new RebaseTemplateVariantInput(null), Corr, CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(localChecksum, f.TemplateVersions.Items.Single().Checksum);
        Assert.Equal(localObjectKey, f.TemplateVersions.Items.Single().FileRef.ObjectKey);
        Assert.Equal(TemplateVariantContentSource.LocalUpload, f.Variants.Items.Single().ContentSource);
    }

    [Fact]
    public async Task Rebase_missing_variant_returns_not_found_non_leakage()
    {
        var f = Fixture();

        var response = await f.Service.RebaseAsync(Guid.NewGuid(), new RebaseTemplateVariantInput(null), Corr, CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
        Assert.Equal(TemplateVariantReasonCodes.NotFoundNonLeakage, response.ReasonCode);
    }

    [Fact]
    public async Task Rebase_blocked_when_master_deprecated_leaves_metadata_unchanged()
    {
        var f = Fixture();
        var (master, _) = SeedPublishedMaster(f, currentVersion: 1);
        var created = await f.Service.CreateAsync(Input(master), Corr, CancellationToken.None);
        var before = f.Variants.Items.Single();
        before.HasLocalChanges = true;
        master.Status = TemplateMasterStatus.Deprecated;

        var response = await f.Service.RebaseAsync(created.Data!.Id, new RebaseTemplateVariantInput(null), Corr, CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Equal(TemplateVariantReasonCodes.RebaseBlocked, response.ReasonCode);
        Assert.True(f.Variants.Items.Single().HasLocalChanges);
    }

    [Fact]
    public async Task List_projects_derived_master_and_computed_drift()
    {
        var f = Fixture();
        var (master, _) = SeedPublishedMaster(f, currentVersion: 1);
        await f.Service.CreateAsync(Input(master), Corr, CancellationToken.None);

        var response = await f.Service.ListAsync(new TemplateVariantListFilter(null, null, null, null, null), Corr, CancellationToken.None);

        Assert.True(response.IsSuccessful);
        var item = Assert.Single(response.Data!);
        Assert.Equal(master.MasterCode, item.MasterCode);
        Assert.Equal("INSYNC", item.DriftStatus);
        Assert.Equal("Company", item.ScopeType);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Harness Fixture(bool enforceTargetFolderPermission = false)
    {
        var tenant = new TenantContext();
        tenant.SetTenant(TenantId);
        var variants = new FakeTemplateVariantRepository();
        var masters = new FakeTemplateMasterRepository();
        var versions = new FakeTemplateMasterVersionRepository();
        var templateDocuments = new FakeTemplateDocumentRepository();
        var templateVersions = new FakeTemplateVersionRepository();
        var folders = new FakeCollectionInstanceReferenceReader();
        var storage = new FakeContentStorageGateway();
        folders.Items.Add(new CollectionInstanceReferenceDto(FolderId, ScopeId, Guid.NewGuid(), "manuals", "quality", "Manuals", "Quality/Manuals", "ACTIVE", true, []));
        var access = enforceTargetFolderPermission
            ? new DocumentAccessEvaluator(
                new FakeFolderDocumentAccessPolicyRepository(),
                new FakeDocumentShareRecordRepository(),
                new FakePrincipalAccessor(new DocumentPrincipal(Guid.NewGuid(), [], [ScopeId])))
            : null;
        var service = new TemplateVariantService(
            variants,
            masters,
            versions,
            templateDocuments,
            templateVersions,
            folders,
            new DocumentKeyFactory(),
            new DocumentVersioningService(storage, tenant),
            tenant,
            new FakeCurrentUserContext(),
            access);
        return new Harness(service, variants, masters, versions, templateDocuments, templateVersions, folders, storage);
    }

    private static (TemplateMaster master, TemplateMasterVersion version) SeedPublishedMaster(Harness f, int currentVersion)
    {
        var version = new TemplateMasterVersion
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            TemplateMasterId = Guid.NewGuid(),
            VersionNumber = currentVersion,
            FileRef = Content("c1"),
            Checksum = "c1"
        };
        var master = new TemplateMaster
        {
            Id = version.TemplateMasterId,
            TenantId = TenantId,
            MasterCode = "QMS-SOP-001",
            TemplateName = "QMS SOP Master",
            Classification = "SOP",
            Status = TemplateMasterStatus.Published,
            CurrentVersionId = version.Id,
            CurrentMasterVersion = currentVersion
        };
        f.Masters.Items.Add(master);
        f.MasterVersions.Items.Add(version);
        return (master, version);
    }

    private static TemplateMaster NewMaster(int currentVersion, TemplateMasterStatus status) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = TenantId,
        MasterCode = "M",
        TemplateName = "M",
        Classification = "SOP",
        Status = status,
        CurrentMasterVersion = currentVersion
    };

    private static TemplateVariant NewVariant(TemplateMaster master, int? rebasedNumber, bool hasLocalChanges) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = TenantId,
        TemplateMasterId = master.Id,
        TemplateMasterVersionId = Guid.NewGuid(),
        VariantCode = "V",
        VariantName = "V",
        ScopeType = TemplateVariantScopeType.Company,
        ScopeId = ScopeId,
        LastRebasedMasterVersionNumber = rebasedNumber,
        HasLocalChanges = hasLocalChanges
    };

    private static CreateTemplateVariantInput Input(TemplateMaster master) => new(
        master.Id,
        master.CurrentVersionId!.Value,
        "COMP-SOP-001",
        "Company SOP Variant",
        "Company-scoped variant",
        "COMPANY",
        ScopeId,
        FolderId,
        "MASTER_VERSION",
        null,
        null,
        null,
        null);

    private sealed record Harness(
        TemplateVariantService Service,
        FakeTemplateVariantRepository Variants,
        FakeTemplateMasterRepository Masters,
        FakeTemplateMasterVersionRepository MasterVersions,
        FakeTemplateDocumentRepository TemplateDocuments,
        FakeTemplateVersionRepository TemplateVersions,
        FakeCollectionInstanceReferenceReader Folders,
        FakeContentStorageGateway Storage);

    private static FileUploadInput LocalFile(string fileName = "variant.docx", string content = "variant-content") =>
        new(fileName, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(content)));

    private static ContentRef Content(string checksum) => new()
    {
        ContentId = Guid.NewGuid(),
        StorageProvider = "fake",
        ObjectKey = $"objects/{checksum}",
        FileName = "master.docx",
        MediaType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ByteSize = 12,
        Checksum = checksum,
        VersionId = Guid.NewGuid(),
        CreatedBy = "seed"
    };

    private sealed class FakeTemplateVariantRepository : ITemplateVariantRepository
    {
        public List<TemplateVariant> Items { get; } = [];

        public Task<TemplateVariant> CreateAsync(TemplateVariant variant, CancellationToken ct = default)
        {
            if (Items.Any(x => x.ScopeType == variant.ScopeType && x.ScopeId == variant.ScopeId && x.VariantCode == variant.VariantCode && !x.IsDeleted))
            {
                throw new InvalidOperationException("duplicate");
            }

            Items.Add(variant);
            return Task.FromResult(variant);
        }

        public Task<TemplateVariant?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => x.Id == id && !x.IsDeleted));

        public Task<TemplateVariant?> GetByScopeAndCodeAsync(TemplateVariantScopeType scopeType, Guid scopeId, string variantCode, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => x.ScopeType == scopeType && x.ScopeId == scopeId && x.VariantCode == variantCode && !x.IsDeleted));

        public Task<IReadOnlyList<TemplateVariant>> ListAsync(Guid? templateMasterId, string? scopeType, Guid? scopeId, string? status, string? approvalStatus, CancellationToken ct = default)
        {
            IEnumerable<TemplateVariant> query = Items.Where(x => !x.IsDeleted);
            if (templateMasterId is { } mid && mid != Guid.Empty) query = query.Where(x => x.TemplateMasterId == mid);
            if (Enum.TryParse<TemplateVariantScopeType>(scopeType, true, out var st)) query = query.Where(x => x.ScopeType == st);
            if (scopeId is { } sid && sid != Guid.Empty) query = query.Where(x => x.ScopeId == sid);
            if (Enum.TryParse<TemplateVariantStatus>(status, true, out var ps)) query = query.Where(x => x.Status == ps);
            if (Enum.TryParse<TemplateVariantApprovalStatus>(approvalStatus, true, out var pa)) query = query.Where(x => x.ApprovalStatus == pa);
            return Task.FromResult<IReadOnlyList<TemplateVariant>>(query.ToList());
        }

        public Task<IReadOnlyList<TemplateVariant>> GetByMasterAsync(Guid templateMasterId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TemplateVariant>>(Items.Where(x => x.TemplateMasterId == templateMasterId && !x.IsDeleted).ToList());

        public Task<bool> UpdateAsync(TemplateVariant variant, CancellationToken ct = default)
        {
            var index = Items.FindIndex(x => x.Id == variant.Id);
            if (index >= 0) Items[index] = variant;
            return Task.FromResult(index >= 0);
        }

        public Task SoftDeleteAsync(Guid id, CancellationToken ct = default)
        {
            var variant = Items.FirstOrDefault(x => x.Id == id && !x.IsDeleted);
            if (variant is not null) variant.IsDeleted = true;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTemplateMasterRepository : ITemplateMasterRepository
    {
        public List<TemplateMaster> Items { get; } = [];

        public Task<TemplateMaster> CreateAsync(TemplateMaster master, CancellationToken ct = default) { Items.Add(master); return Task.FromResult(master); }
        public Task<TemplateMaster?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(x => x.Id == id && !x.IsDeleted));
        public Task<TemplateMaster?> GetByMasterCodeAsync(string masterCode, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(x => x.MasterCode == masterCode && !x.IsDeleted));

        public Task<IReadOnlyList<TemplateMaster>> ListAsync(string? status, string? classification, Guid? collectionDefinitionId, string? canonicalId, string? variantPolicy, CancellationToken ct = default)
        {
            IEnumerable<TemplateMaster> query = Items.Where(x => !x.IsDeleted);
            if (Enum.TryParse<TemplateMasterStatus>(status, true, out var ps)) query = query.Where(x => x.Status == ps);
            return Task.FromResult<IReadOnlyList<TemplateMaster>>(query.ToList());
        }

        public Task<bool> UpdateAsync(TemplateMaster master, CancellationToken ct = default)
        {
            var index = Items.FindIndex(x => x.Id == master.Id);
            if (index >= 0) Items[index] = master;
            return Task.FromResult(index >= 0);
        }

        public Task<int> CountByCurrentVersionAsync(Guid templateMasterVersionId, CancellationToken ct = default) =>
            Task.FromResult(Items.Count(x => x.CurrentVersionId == templateMasterVersionId && !x.IsDeleted));

        public Task SoftDeleteAsync(Guid id, CancellationToken ct = default)
        {
            var master = Items.FirstOrDefault(x => x.Id == id && !x.IsDeleted);
            if (master is not null) master.IsDeleted = true;
            return Task.CompletedTask;
        }

        public Task<int> BulkSoftDeleteAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default)
        {
            var set = ids.ToHashSet();
            var affected = Items.Where(x => set.Contains(x.Id) && !x.IsDeleted).ToList();
            foreach (var master in affected) master.IsDeleted = true;
            return Task.FromResult(affected.Count);
        }
    }

    private sealed class FakeTemplateMasterVersionRepository : ITemplateMasterVersionRepository
    {
        public List<TemplateMasterVersion> Items { get; } = [];

        public Task<TemplateMasterVersion> CreateAsync(TemplateMasterVersion version, CancellationToken ct = default) { Items.Add(version); return Task.FromResult(version); }
        public Task<TemplateMasterVersion?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(x => x.Id == id && !x.IsDeleted));

        public Task<IReadOnlyList<TemplateMasterVersion>> GetByMasterAsync(Guid templateMasterId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TemplateMasterVersion>>(Items.Where(x => x.TemplateMasterId == templateMasterId && !x.IsDeleted).OrderByDescending(x => x.VersionNumber).ToList());

        public Task<TemplateMasterVersion?> GetByMasterAndNumberAsync(Guid templateMasterId, int versionNumber, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => x.TemplateMasterId == templateMasterId && x.VersionNumber == versionNumber && !x.IsDeleted));

        public Task<int> GetMaxVersionNumberAsync(Guid templateMasterId, CancellationToken ct = default) =>
            Task.FromResult(Items.Where(x => x.TemplateMasterId == templateMasterId && !x.IsDeleted).Select(x => x.VersionNumber).DefaultIfEmpty(0).Max());

        public Task SupersedePublishedVersionsAsync(Guid templateMasterId, Guid exceptVersionId, CancellationToken ct = default) => Task.CompletedTask;

        public Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            var version = Items.FirstOrDefault(x => x.Id == id);
            if (version is not null) version.IsDeleted = true;
            return Task.CompletedTask;
        }
    }
}
