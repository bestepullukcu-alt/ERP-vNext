using Diten.Platform.Application.Features.DocumentManagementControlledDocuments;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
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

        var second = await f.Documents.CreateVersionAsync(docId, File("v2"), "second", Corr, CancellationToken.None);

        Assert.True(second.IsSuccessful);
        Assert.Equal(2, second.Data!.VersionNumber);
        Assert.Equal(2, f.VersionRepo.Items.Count);
        Assert.Equal(DocumentVersionStatus.Superseded, f.VersionRepo.Items.Single(v => v.VersionNumber == 1).VersionStatus);
        Assert.Equal(DocumentVersionStatus.Active, f.VersionRepo.Items.Single(v => v.VersionNumber == 2).VersionStatus);
        Assert.Equal(docId, f.DocumentRepo.Items.Single().Id);
        Assert.Equal(2, f.DocumentRepo.Items.Single().CurrentVersionNumber);
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
        TestFixture? shareReader = null)
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

        if (grantFolder)
        {
            folderPolicies.Items.Add(new FolderDocumentAccessPolicy
            {
                TenantId = TenantId,
                CollectionInstanceId = InstanceId,
                CompanyId = CompanyA,
                TargetType = AccessTargetType.Company,
                TargetId = CompanyA.ToString("D"),
                FolderPermissions = new FolderPermissionSet
                {
                    CanViewFolderDocuments = true,
                    CanUploadDocument = true,
                    CanEditFolderDocuments = true,
                    CanUploadNewVersion = true,
                    CanShareFolderDocuments = true,
                    CanManageFolderDocumentAccess = true
                }
            });
        }

        var principal = new DocumentPrincipal(Guid.NewGuid(), [], principalCompanies ?? [CompanyA]);
        var access = new DocumentAccessEvaluator(folderPolicies, shares, new FakePrincipalAccessor(principal));
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

        var documents = new ControlledDocumentService(reader, documentRepo, versionRepo, shares, versioning, access, keyFactory, currentUser, tenantContext, flags);
        var templates = new TemplateService(reader, templateRepo, templateVersionRepo, shares, versioning, access, keyFactory, currentUser, tenantContext, flags);
        var sharing = new TemplateSharingService(documentRepo, versionRepo, templateRepo, templateVersionRepo, shares, legalEntity, access, currentUser, tenantContext, flags);
        var planner = new FolderSharePlanner(reader, templateRepo, legalEntity, access, tenantContext);
        var folderShares = new FolderShareService(planner, sharing, folderOps, folderOutcomes, currentUser, tenantContext);

        return new TestFixture(documents, templates, sharing, folderShares, documentRepo, versionRepo, templateRepo, templateVersionRepo, shares, folderOps, folderOutcomes, storage);
    }

    private sealed record TestFixture(
        ControlledDocumentService Documents,
        TemplateService Templates,
        TemplateSharingService Sharing,
        FolderShareService FolderShares,
        FakeControlledDocumentRepository DocumentRepo,
        FakeControlledDocumentVersionRepository VersionRepo,
        FakeTemplateDocumentRepository TemplateRepo,
        FakeTemplateVersionRepository TemplateVersionRepo,
        FakeDocumentShareRecordRepository ShareRepo,
        FakeFolderShareOperationRepository FolderOpRepo,
        FakeFolderShareOutcomeRepository FolderOutcomeRepo,
        FakeContentStorageGateway Storage);
}
