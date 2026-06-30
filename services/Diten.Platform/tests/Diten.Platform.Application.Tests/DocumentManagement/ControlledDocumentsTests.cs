using Diten.Platform.Application.Features.DocumentManagementAccessMatrix;
using Diten.Platform.Application.Features.DocumentManagementAccessMatrix.Services;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Platform.Application.Tests.DocumentManagement;

public sealed class ControlledDocumentsTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CompanyA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid CompanyB = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
    private static readonly Guid InstanceId = Guid.Parse("cccccccc-0000-0000-0000-000000000003");
    private const string Corr = "fu01-corr-1";

    [Fact]
    public async Task Create_attaches_document_to_valid_collection_instance_with_first_active_version()
    {
        var f = Fixture(grantFolder: true);

        var response = await f.Documents.CreateAsync(CreateInput(), Corr, CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(201, response.StatusCode);
        Assert.Equal(Corr, response.CorrelationId);
        var doc = Assert.Single(f.DocumentRepo.Items);
        Assert.Equal(CompanyA, doc.OwnerCompanyId);
        Assert.Equal(InstanceId, doc.CollectionInstanceId);
        Assert.Equal("Quality/Manuals", doc.CollectionPath);
        var version = Assert.Single(f.VersionRepo.Items);
        Assert.Equal(1, version.VersionNumber);
        Assert.Equal(DocumentVersionStatus.Active, version.VersionStatus);
        Assert.Single(f.Storage.Stored);
    }

    [Fact]
    public async Task Create_missing_collection_instance_returns_404_non_leakage_and_writes_nothing()
    {
        var f = Fixture(grantFolder: true);
        var input = CreateInput() with { CollectionInstanceId = Guid.NewGuid() };

        var response = await f.Documents.CreateAsync(input, Corr, CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
        Assert.Equal(ControlledDocumentReasonCodes.NotFoundNonLeakage, response.ReasonCode);
        Assert.Empty(f.DocumentRepo.Items);
        Assert.Empty(f.Storage.Stored);
    }

    [Fact]
    public async Task Create_without_folder_upload_permission_is_denied_403()
    {
        var f = Fixture(grantFolder: false);

        var response = await f.Documents.CreateAsync(CreateInput(), Corr, CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(403, response.StatusCode);
        Assert.Equal(ControlledDocumentReasonCodes.PermissionDenied, response.ReasonCode);
        Assert.Empty(f.DocumentRepo.Items);
    }

    [Fact]
    public async Task Storage_unavailable_leaves_no_metadata_orphan()
    {
        var f = Fixture(grantFolder: true);
        f.Storage.FailStore = true;

        var response = await f.Documents.CreateAsync(CreateInput(), Corr, CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(503, response.StatusCode);
        Assert.Equal(ControlledDocumentReasonCodes.StorageUnavailable, response.ReasonCode);
        Assert.Empty(f.DocumentRepo.Items);
        Assert.Empty(f.VersionRepo.Items);
    }

    [Fact]
    public async Task Metadata_commit_failure_best_effort_deletes_stored_content()
    {
        var f = Fixture(grantFolder: true);
        f.VersionRepo.FailCreate = true;

        var response = await f.Documents.CreateAsync(CreateInput(), Corr, CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Single(f.Storage.Stored);
        Assert.Single(f.Storage.Deleted); // compensating delete attempted (no orphan content)
    }

    [Fact]
    public async Task Second_version_supersedes_first_and_keeps_both_immutable_rows()
    {
        var f = Fixture(grantFolder: true);
        var created = await f.Documents.CreateAsync(CreateInput(), Corr, CancellationToken.None);
        var docId = created.Data!.Id;

        var second = await f.Documents.CreateVersionAsync(docId, File("v2"), "second", false, Corr, CancellationToken.None);

        Assert.True(second.IsSuccessful);
        Assert.Equal(2, second.Data!.VersionNumber);
        Assert.Equal(2, f.VersionRepo.Items.Count);
        Assert.Equal(DocumentVersionStatus.Superseded, f.VersionRepo.Items.Single(v => v.VersionNumber == 1).VersionStatus);
        Assert.Equal(DocumentVersionStatus.Active, f.VersionRepo.Items.Single(v => v.VersionNumber == 2).VersionStatus);
        Assert.Equal(docId, f.DocumentRepo.Items.Single().Id);
        Assert.Equal(2, f.DocumentRepo.Items.Single().CurrentVersionNumber);
    }

    [Fact]
    public async Task New_version_identical_to_active_is_rejected_with_no_content_change()
    {
        var f = Fixture(grantFolder: true);
        var created = await f.Documents.CreateAsync(CreateInput(), Corr, CancellationToken.None);
        var docId = created.Data!.Id;

        // Re-upload byte-identical content to the initial active version (File("v1")) without forcing.
        var response = await f.Documents.CreateVersionAsync(docId, File("v1"), "no real change", false, Corr, CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Equal(ControlledDocumentReasonCodes.NoContentChange, response.ReasonCode);
        Assert.Single(f.VersionRepo.Items);            // no second version row
        Assert.Single(f.Storage.Stored);               // no second storage write (no orphan)
        Assert.Empty(f.Storage.Deleted);
    }

    [Fact]
    public async Task New_version_identical_to_active_is_allowed_when_explicitly_forced()
    {
        var f = Fixture(grantFolder: true);
        var created = await f.Documents.CreateAsync(CreateInput(), Corr, CancellationToken.None);
        var docId = created.Data!.Id;

        var response = await f.Documents.CreateVersionAsync(docId, File("v1"), "intentional re-version", true, Corr, CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(2, response.Data!.VersionNumber);
        Assert.Equal(2, f.VersionRepo.Items.Count);
    }

    [Fact]
    public async Task Download_without_layer2_grant_is_denied_for_cross_company_principal()
    {
        var f = Fixture(grantFolder: true);
        var created = await f.Documents.CreateAsync(CreateInput(), Corr, CancellationToken.None);
        var docId = created.Data!.Id;
        var versionId = f.VersionRepo.Items.Single().Id;

        // A principal from another company with no share cannot even reach the document.
        var other = Fixture(grantFolder: true, principalCompanies: [CompanyB], shareReader: f);
        var response = await other.Documents.DownloadAsync(docId, versionId, Corr, CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
        Assert.Equal(ControlledDocumentReasonCodes.NotFoundNonLeakage, response.ReasonCode);
    }

    [Fact]
    public async Task Non_shareable_template_cannot_be_shared()
    {
        var f = Fixture(grantFolder: true, referenceable: true);
        var template = SeedTemplate(f, shareable: false);

        var response = await f.Sharing.ShareTemplateAsync(template.Id, CompanyB, DocumentShareMode.Reference, Corr, CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Equal(ControlledDocumentReasonCodes.ValidationFailed, response.ReasonCode);
        Assert.Empty(f.ShareRepo.Items);
    }

    [Fact]
    public async Task Reference_share_creates_record_and_makes_target_company_reach_the_document()
    {
        var f = Fixture(grantFolder: true, referenceable: true);
        var created = await f.Documents.CreateAsync(CreateInput(), Corr, CancellationToken.None);
        var docId = created.Data!.Id;

        var share = await f.Sharing.ShareDocumentAsync(docId, CompanyB, DocumentShareMode.Reference, Corr, CancellationToken.None);

        Assert.True(share.IsSuccessful);
        Assert.Equal("REFERENCE", share.Data!.ShareMode);
        Assert.Null(share.Data.CopiedItemId);
        var record = Assert.Single(f.ShareRepo.Items);
        Assert.Equal(CompanyB, record.TargetCompanyId);
    }

    [Fact]
    public async Task Share_target_not_referenceable_fails_closed_404_without_writes()
    {
        var f = Fixture(grantFolder: true, referenceable: false);
        var created = await f.Documents.CreateAsync(CreateInput(), Corr, CancellationToken.None);

        var share = await f.Sharing.ShareDocumentAsync(created.Data!.Id, CompanyB, DocumentShareMode.Reference, Corr, CancellationToken.None);

        Assert.False(share.IsSuccessful);
        Assert.Equal(404, share.StatusCode);
        Assert.Equal(ControlledDocumentReasonCodes.NotFoundNonLeakage, share.ReasonCode);
        Assert.Empty(f.ShareRepo.Items);
    }

    [Fact]
    public async Task Copy_on_adopt_blocked_when_feature_flag_disabled()
    {
        var f = Fixture(grantFolder: true, referenceable: true, copyEnabled: false);
        var template = SeedTemplate(f, shareable: true, copyable: true);

        var response = await f.Sharing.ShareTemplateAsync(template.Id, CompanyB, DocumentShareMode.CopyOnAdopt, Corr, CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(403, response.StatusCode);
        Assert.Empty(f.ShareRepo.Items);
    }

    [Fact]
    public async Task Folder_share_dry_run_mutates_nothing_and_lists_included_templates()
    {
        var f = Fixture(grantFolder: true, referenceable: true);
        SeedTemplate(f, shareable: true, instanceId: InstanceId);
        SeedTemplate(f, shareable: false, instanceId: InstanceId);

        var response = await f.FolderShares.DryRunAsync(new FolderShareInput(InstanceId, CompanyB, true, "REFERENCE"), Corr, CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal("DRY_RUN", response.Data!.OperationType);
        Assert.Equal(1, response.Data.TemplatesIncluded);
        Assert.Equal(1, response.Data.TemplatesSkipped);
        Assert.Empty(f.FolderOpRepo.Items);
        Assert.Empty(f.ShareRepo.Items);
    }

    [Fact]
    public async Task Folder_share_execute_shares_only_included_templates_and_records_outcomes()
    {
        var f = Fixture(grantFolder: true, referenceable: true);
        SeedTemplate(f, shareable: true, instanceId: InstanceId);

        var response = await f.FolderShares.ExecuteAsync(new FolderShareInput(InstanceId, CompanyB, true, "REFERENCE"), Corr, CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(201, response.StatusCode);
        Assert.Equal("EXECUTE", response.Data!.OperationType);
        Assert.Equal(1, response.Data.TemplatesIncluded);
        Assert.Single(f.FolderOpRepo.Items);
        Assert.Single(f.ShareRepo.Items, s => s.TargetCompanyId == CompanyB && s.ItemKind == SharedItemKind.Template);
    }

    [Fact]
    public async Task Folder_share_invalid_target_company_is_rejected()
    {
        var f = Fixture(grantFolder: true, referenceable: false);
        SeedTemplate(f, shareable: true, instanceId: InstanceId);

        var response = await f.FolderShares.ExecuteAsync(new FolderShareInput(InstanceId, CompanyB, true, "REFERENCE"), Corr, CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
        Assert.Empty(f.FolderOpRepo.Items);
    }

    [Fact]
    public async Task Company_claimless_principal_with_folder_view_grant_can_list_and_reach_document()
    {
        // Seed-admin JWT shape: a user id (sub) but NO company claim; visibility relies on a user/role folder grant.
        var userId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var f = Fixture(grantFolder: true, folderGrantUserId: userId);

        var created = await f.Documents.CreateAsync(CreateInput(), Corr, CancellationToken.None);
        Assert.True(created.IsSuccessful);

        var list = await f.Documents.ListAsync(null, Corr, CancellationToken.None);
        Assert.True(list.IsSuccessful);
        Assert.Single(list.Data!); // folder-view fallback makes it visible without a company claim

        var detail = await f.Documents.GetDetailAsync(created.Data!.Id, Corr, CancellationToken.None);
        Assert.True(detail.IsSuccessful);

        var folderDocs = await f.FolderDocs.GetFolderDocumentsAsync(InstanceId, Corr, CancellationToken.None);
        Assert.True(folderDocs.IsSuccessful);
        Assert.Single(folderDocs.Data!.Documents);
    }

    [Fact]
    public async Task Company_claimless_principal_without_grant_sees_documents_in_compatibility_deny_only()
    {
        // FU04 Deny-only rollout: a token with no company claim (seed-admin shape) is a tenant-wide viewer in
        // Compatibility mode — visibility no longer requires owner-company membership or a folder grant.
        var owner = Fixture(grantFolder: true); // claimed principal seeds the document
        var created = await owner.Documents.CreateAsync(CreateInput(), Corr, CancellationToken.None);
        Assert.True(created.IsSuccessful);

        var claimless = Fixture(grantFolder: false, principalCompanies: [], shareReader: owner);
        var list = await claimless.Documents.ListAsync(null, Corr, CancellationToken.None);
        var folderDocs = await claimless.FolderDocs.GetFolderDocumentsAsync(InstanceId, Corr, CancellationToken.None);

        Assert.True(list.IsSuccessful);
        Assert.Single(list.Data!);
        Assert.True(folderDocs.IsSuccessful);
        Assert.Single(folderDocs.Data!.Documents);
    }

    [Fact]
    public async Task Controlled_document_item_view_deny_hides_from_lists_even_with_folder_allow()
    {
        var f = Fixture(grantFolder: true);
        var created = await f.Documents.CreateAsync(CreateInput(), Corr, CancellationToken.None);
        Assert.True(created.IsSuccessful);
        SeedMatrixPolicy(f, DocumentAccessTargetType.ControlledDocument, created.Data!.Id, DocumentAccessEffect.Deny, DocumentAccessMatrixAction.View);

        var list = await f.Documents.ListAsync(null, Corr, CancellationToken.None);
        var folderDocs = await f.FolderDocs.GetFolderDocumentsAsync(InstanceId, Corr, CancellationToken.None);
        var detail = await f.Documents.GetDetailAsync(created.Data.Id, Corr, CancellationToken.None);

        Assert.True(list.IsSuccessful);
        Assert.Empty(list.Data!);
        Assert.True(folderDocs.IsSuccessful);
        Assert.Empty(folderDocs.Data!.Documents);
        Assert.False(detail.IsSuccessful);
        Assert.Equal(404, detail.StatusCode);
    }

    [Fact]
    public async Task Controlled_document_action_deny_returns_403_when_view_is_transitionally_allowed()
    {
        var f = Fixture(grantFolder: true);
        var created = await f.Documents.CreateAsync(CreateInput(), Corr, CancellationToken.None);
        Assert.True(created.IsSuccessful);
        var docId = created.Data!.Id;
        var versionId = f.VersionRepo.Items.Single().Id;
        SeedMatrixPolicy(f, DocumentAccessTargetType.ControlledDocument, docId, DocumentAccessEffect.Deny,
            DocumentAccessMatrixAction.Download,
            DocumentAccessMatrixAction.EditMetadata,
            DocumentAccessMatrixAction.UploadVersion,
            DocumentAccessMatrixAction.Archive,
            DocumentAccessMatrixAction.Share);

        var download = await f.Documents.DownloadAsync(docId, versionId, Corr, CancellationToken.None);
        var edit = await f.Documents.EditMetadataAsync(docId, new EditControlledDocumentInput("Denied", null, [], null, null, null), Corr, CancellationToken.None);
        var upload = await f.Documents.CreateVersionAsync(docId, File("v2"), "blocked", false, Corr, CancellationToken.None);
        var archive = await f.Documents.DeleteAsync(docId, Corr, CancellationToken.None);
        var share = await f.Sharing.ShareDocumentAsync(docId, CompanyB, DocumentShareMode.Reference, Corr, CancellationToken.None);

        Assert.Equal(403, download.StatusCode);
        Assert.Equal(403, edit.StatusCode);
        Assert.Equal(403, upload.StatusCode);
        Assert.Equal(403, archive.StatusCode);
        Assert.Equal(403, share.StatusCode);
    }

    [Fact]
    public async Task Template_document_item_view_deny_hides_from_lists_even_with_folder_allow()
    {
        var f = Fixture(grantFolder: true);
        var template = SeedTemplate(f, shareable: true, instanceId: InstanceId);
        SeedMatrixPolicy(f, DocumentAccessTargetType.TemplateDocument, template.Id, DocumentAccessEffect.Deny, DocumentAccessMatrixAction.View);

        var list = await f.Templates.ListAsync(null, Corr, CancellationToken.None);
        var folderDocs = await f.FolderDocs.GetFolderDocumentsAsync(InstanceId, Corr, CancellationToken.None);
        var detail = await f.Templates.GetDetailAsync(template.Id, Corr, CancellationToken.None);

        Assert.True(list.IsSuccessful);
        Assert.Empty(list.Data!);
        Assert.True(folderDocs.IsSuccessful);
        Assert.Empty(folderDocs.Data!.Templates);
        Assert.False(detail.IsSuccessful);
        Assert.Equal(404, detail.StatusCode);
    }

    [Fact]
    public async Task Template_document_action_deny_beats_owner_company_fallback()
    {
        var f = Fixture(grantFolder: true);
        var template = SeedTemplate(f, shareable: true, instanceId: InstanceId);
        SeedMatrixPolicy(f, DocumentAccessTargetType.TemplateDocument, template.Id, DocumentAccessEffect.Deny,
            DocumentAccessMatrixAction.Download,
            DocumentAccessMatrixAction.UploadVersion,
            DocumentAccessMatrixAction.Share);

        var download = await f.Templates.DownloadAsync(template.Id, Guid.NewGuid(), Corr, CancellationToken.None);
        var upload = await f.Templates.CreateVersionAsync(template.Id, File("v2"), "blocked", false, Corr, CancellationToken.None);
        var share = await f.Sharing.ShareTemplateAsync(template.Id, CompanyB, DocumentShareMode.Reference, Corr, CancellationToken.None);

        Assert.Equal(403, download.StatusCode);
        Assert.Equal(403, upload.StatusCode);
        Assert.Equal(403, share.StatusCode);
    }

    [Fact]
    public async Task Transitional_default_without_matrix_policy_keeps_existing_folder_visibility()
    {
        var f = Fixture(grantFolder: true);
        var created = await f.Documents.CreateAsync(CreateInput(), Corr, CancellationToken.None);
        Assert.True(created.IsSuccessful);

        var list = await f.Documents.ListAsync(null, Corr, CancellationToken.None);
        var folderDocs = await f.FolderDocs.GetFolderDocumentsAsync(InstanceId, Corr, CancellationToken.None);

        Assert.Single(list.Data!);
        Assert.Single(folderDocs.Data!.Documents);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static CreateControlledDocumentInput CreateInput() => new(
        InstanceId, CompanyA, "Quality Manual", "SOP", "desc", ["a", "b"], true, null, null, null, File("v1"), "initial", null);

    private static FileUploadInput File(string marker) =>
        new($"{marker}.pdf", "application/pdf", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("content-" + marker)));

    private static TemplateDocument SeedTemplate(TestFixture f, bool shareable, bool copyable = false, Guid? instanceId = null)
    {
        var template = new TemplateDocument
        {
            TenantId = TenantId,
            TemplateKey = $"tmpl-{Guid.NewGuid():N}",
            CompanyId = CompanyA,
            OwnerCompanyId = CompanyA,
            CollectionInstanceId = instanceId,
            CollectionPath = instanceId is null ? null : "Quality/Manuals",
            Title = "Form Template",
            TemplateFlags = new TemplateFlags { Reusable = true, Shareable = shareable, CopyableOnAdopt = copyable, ReferenceOnly = !shareable },
            CurrentVersionNumber = 1,
            CreatedBy = "seed"
        };
        f.TemplateRepo.Items.Add(template);
        return template;
    }

    private static TestFixture Fixture(
        bool grantFolder,
        bool referenceable = true,
        bool copyEnabled = false,
        IReadOnlyCollection<Guid>? principalCompanies = null,
        TestFixture? shareReader = null,
        Guid? folderGrantUserId = null)
    {
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(TenantId);

        var reader = new FakeCollectionInstanceReferenceReader();
        reader.Items.Add(new CollectionInstanceReferenceDto(InstanceId, CompanyA, Guid.NewGuid(), "manuals", "quality", "Manuals", "Quality/Manuals", "ACTIVE", true, []));

        var documentRepo = shareReader?.DocumentRepo ?? new FakeControlledDocumentRepository();
        var versionRepo = shareReader?.VersionRepo ?? new FakeControlledDocumentVersionRepository();
        var templateRepo = shareReader?.TemplateRepo ?? new FakeTemplateDocumentRepository();
        var templateVersionRepo = shareReader?.TemplateVersionRepo ?? new FakeTemplateVersionRepository();
        var folderPolicies = new FakeFolderDocumentAccessPolicyRepository();
        var shares = shareReader?.ShareRepo ?? new FakeDocumentShareRecordRepository();
        var folderOps = new FakeFolderShareOperationRepository();
        var folderOutcomes = new FakeFolderShareOutcomeRepository();
        var storage = shareReader?.Storage ?? new FakeContentStorageGateway();

        var fullPermissions = new FolderPermissionSet
        {
            CanViewFolderDocuments = true,
            CanUploadDocument = true,
            CanEditFolderDocuments = true,
            CanUploadNewVersion = true,
            CanShareFolderDocuments = true,
            CanManageFolderDocumentAccess = true
        };

        if (grantFolder)
        {
            // Company-level grant (default) or, when folderGrantUserId is set, a user-level grant matching a
            // company-claimless principal (mirrors the seed-admin JWT shape: sub claim, no company claim).
            folderPolicies.Items.Add(new FolderDocumentAccessPolicy
            {
                TenantId = TenantId,
                CollectionInstanceId = InstanceId,
                CompanyId = CompanyA,
                TargetType = folderGrantUserId is null ? AccessTargetType.Company : AccessTargetType.User,
                TargetId = (folderGrantUserId ?? CompanyA).ToString("D"),
                FolderPermissions = fullPermissions
            });
        }

        var principal = folderGrantUserId is { } uid
            ? new DocumentPrincipal(uid, [], principalCompanies ?? [])
            : new DocumentPrincipal(Guid.NewGuid(), [], principalCompanies ?? [CompanyA]);
        var matrixPolicies = new FakeDocumentAccessPolicyRepository();
        var inheritance = new DocumentAccessInheritanceResolver(
            new EmptyTemplateVariantRepository(),
            templateRepo,
            documentRepo,
            new EmptyTemplateMasterRepository(),
            reader,
            tenantContext);
        var compatibility = new DocumentAccessCompatibilityAdapter(folderPolicies);
        var matrix = new DocumentAccessResolver(
            matrixPolicies,
            inheritance,
            compatibility,
            new FakePrincipalAccessor(principal),
            Options.Create(new AccessMatrixOptions()));
        var access = new DocumentAccessEvaluator(folderPolicies, shares, new FakePrincipalAccessor(principal), matrix);
        var flags = Options.Create(new ControlledDocumentsFeatureFlagOptions
        {
            ControlledDocumentsEnabled = true,
            TemplateSharingEnabled = true,
            FolderShareCopyOnAdoptEnabled = copyEnabled
        });
        var versioning = new DocumentVersioningService(storage, tenantContext);
        var keyFactory = new DocumentKeyFactory();
        var currentUser = new FakeCurrentUserContext();
        var legalEntity = new FakeLegalEntityReferenceValidator(referenceable);

        var favoriteRepo = new FakeDocumentFavoriteRepository();
        var documents = new ControlledDocumentService(reader, documentRepo, versionRepo, shares, favoriteRepo, versioning, access, keyFactory, currentUser, tenantContext, flags);
        var templates = new TemplateService(reader, templateRepo, templateVersionRepo, shares, versioning, access, keyFactory, currentUser, tenantContext, flags);
        var sharing = new TemplateSharingService(documentRepo, versionRepo, templateRepo, templateVersionRepo, shares, legalEntity, access, currentUser, tenantContext, flags);
        var planner = new FolderSharePlanner(reader, templateRepo, legalEntity, access, tenantContext);
        var folderShares = new FolderShareService(planner, sharing, folderOps, folderOutcomes, currentUser, tenantContext);
        var folderDocs = new FolderDocumentService(reader, documentRepo, templateRepo, favoriteRepo, folderPolicies, access, currentUser, tenantContext);

        return new TestFixture(documents, templates, sharing, folderShares, folderDocs, documentRepo, versionRepo, templateRepo, templateVersionRepo, shares, folderOps, folderOutcomes, storage, matrixPolicies);
    }

    private static void SeedMatrixPolicy(
        TestFixture f,
        DocumentAccessTargetType targetType,
        Guid targetId,
        DocumentAccessEffect effect,
        params DocumentAccessMatrixAction[] actions) =>
        f.MatrixPolicies.Items.Add(new DocumentAccessPolicyEntry
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            TargetType = targetType,
            TargetId = targetId.ToString("D"),
            PrincipalType = DocumentAccessPrincipalType.Company,
            PrincipalId = CompanyA.ToString("D"),
            Actions = actions.ToList(),
            Effect = effect,
            InheritFromParent = false,
            Status = DocumentAccessPolicyStatus.Active
        });

    private sealed record TestFixture(
        ControlledDocumentService Documents,
        TemplateService Templates,
        TemplateSharingService Sharing,
        FolderShareService FolderShares,
        FolderDocumentService FolderDocs,
        FakeControlledDocumentRepository DocumentRepo,
        FakeControlledDocumentVersionRepository VersionRepo,
        FakeTemplateDocumentRepository TemplateRepo,
        FakeTemplateVersionRepository TemplateVersionRepo,
        FakeDocumentShareRecordRepository ShareRepo,
        FakeFolderShareOperationRepository FolderOpRepo,
        FakeFolderShareOutcomeRepository FolderOutcomeRepo,
        FakeContentStorageGateway Storage,
        FakeDocumentAccessPolicyRepository MatrixPolicies);

    private sealed class FakeDocumentAccessPolicyRepository : IDocumentAccessPolicyRepository
    {
        public List<DocumentAccessPolicyEntry> Items { get; } = [];

        public Task<DocumentAccessPolicyEntry> CreateAsync(DocumentAccessPolicyEntry entry, CancellationToken ct = default)
        {
            Items.Add(entry);
            return Task.FromResult(entry);
        }

        public Task<DocumentAccessPolicyEntry?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => x.Id == id && !x.IsDeleted));

        public Task<IReadOnlyList<DocumentAccessPolicyEntry>> ListAsync(string? targetType, string? targetId, string? principalType, string? principalId, string? effect, string? action, string? status, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentAccessPolicyEntry>>(Items.Where(x => !x.IsDeleted).ToList());

        public Task<IReadOnlyList<DocumentAccessPolicyEntry>> GetByTargetsAsync(IReadOnlyList<(DocumentAccessTargetType TargetType, string TargetId)> targets, CancellationToken ct = default)
        {
            var set = targets.Select(t => $"{t.TargetType}:{t.TargetId}".ToLowerInvariant()).ToHashSet();
            return Task.FromResult<IReadOnlyList<DocumentAccessPolicyEntry>>(
                Items.Where(x => !x.IsDeleted && set.Contains($"{x.TargetType}:{x.TargetId}".ToLowerInvariant())).ToList());
        }

        public Task<DocumentAccessPolicyEntry?> FindDuplicateAsync(DocumentAccessTargetType targetType, string targetId, DocumentAccessPrincipalType principalType, string principalId, DocumentAccessEffect effect, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => !x.IsDeleted && x.TargetType == targetType && x.TargetId == targetId && x.PrincipalType == principalType && x.PrincipalId == principalId && x.Effect == effect));

        public Task<bool> UpdateAsync(DocumentAccessPolicyEntry entry, CancellationToken ct = default)
        {
            var i = Items.FindIndex(x => x.Id == entry.Id);
            if (i >= 0) Items[i] = entry;
            return Task.FromResult(i >= 0);
        }

        public Task SoftDeleteAsync(Guid id, CancellationToken ct = default)
        {
            var entry = Items.FirstOrDefault(x => x.Id == id && !x.IsDeleted);
            if (entry is not null)
            {
                entry.IsDeleted = true;
            }

            return Task.CompletedTask;
        }

        public Task<int> BulkSoftDeleteAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default)
        {
            var set = ids.ToHashSet();
            var affected = Items.Where(x => set.Contains(x.Id) && !x.IsDeleted).ToList();
            foreach (var entry in affected)
            {
                entry.IsDeleted = true;
            }

            return Task.FromResult(affected.Count);
        }
    }

    private sealed class EmptyTemplateVariantRepository : ITemplateVariantRepository
    {
        public Task<TemplateVariant> CreateAsync(TemplateVariant v, CancellationToken ct = default) => Task.FromResult(v);
        public Task<TemplateVariant?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<TemplateVariant?>(null);
        public Task<TemplateVariant?> GetByScopeAndCodeAsync(TemplateVariantScopeType scopeType, Guid scopeId, string variantCode, CancellationToken ct = default) => Task.FromResult<TemplateVariant?>(null);
        public Task<IReadOnlyList<TemplateVariant>> ListAsync(Guid? templateMasterId, string? scopeType, Guid? scopeId, string? status, string? approvalStatus, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<TemplateVariant>>([]);
        public Task<IReadOnlyList<TemplateVariant>> GetByMasterAsync(Guid templateMasterId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<TemplateVariant>>([]);
        public Task<bool> UpdateAsync(TemplateVariant v, CancellationToken ct = default) => Task.FromResult(true);
        public Task SoftDeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class EmptyTemplateMasterRepository : ITemplateMasterRepository
    {
        public Task<TemplateMaster> CreateAsync(TemplateMaster m, CancellationToken ct = default) => Task.FromResult(m);
        public Task<TemplateMaster?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<TemplateMaster?>(null);
        public Task<TemplateMaster?> GetByMasterCodeAsync(string masterCode, CancellationToken ct = default) => Task.FromResult<TemplateMaster?>(null);
        public Task<IReadOnlyList<TemplateMaster>> ListAsync(
            string? status,
            string? classification,
            Guid? collectionDefinitionId,
            string? canonicalId,
            string? variantPolicy,
            CancellationToken ct = default) => Task.FromResult<IReadOnlyList<TemplateMaster>>([]);
        public Task<bool> UpdateAsync(TemplateMaster m, CancellationToken ct = default) => Task.FromResult(true);
        public Task<int> CountByCurrentVersionAsync(Guid templateMasterVersionId, CancellationToken ct = default) => Task.FromResult(0);
        public Task SoftDeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> BulkSoftDeleteAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default) => Task.FromResult(0);
    }
}
