using Diten.Platform.Application.Features.DocumentManagementControlledDocuments;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Services;
using Diten.Platform.Application.Features.DocumentManagementMasterRegister;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Platform.Application.Tests.DocumentManagement;

public sealed class ControlledDocumentsExplorerTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CompanyA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid CompanyB = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
    private static readonly Guid B1 = Guid.Parse("dddddddd-0000-0000-0000-000000000001");
    private static readonly Guid B2 = Guid.Parse("dddddddd-0000-0000-0000-000000000002");
    // Quality structure (B1): root + Manuals + Forms; Safety structure (B2): root.
    private static readonly Guid QualityRoot = Guid.Parse("cc000000-0000-0000-0000-000000000001");
    private static readonly Guid Manuals = Guid.Parse("cc000000-0000-0000-0000-000000000002");
    private static readonly Guid Forms = Guid.Parse("cc000000-0000-0000-0000-000000000003");
    private static readonly Guid SafetyRoot = Guid.Parse("cc000000-0000-0000-0000-000000000004");
    private const string Corr = "fu01-exp-1";

    [Fact]
    public async Task Active_structures_returns_one_per_instantiated_baseline_for_company()
    {
        var f = Build();
        var response = await f.Explorer.GetActiveStructuresAsync(CompanyA, Corr, CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(2, response.Data!.Count); // Quality (B1) + Safety (B2)
        Assert.Contains(response.Data, s => s.RootCollectionInstanceId == QualityRoot && s.BaselineReleaseId == B1);
        Assert.Contains(response.Data, s => s.RootCollectionInstanceId == SafetyRoot && s.BaselineReleaseId == B2);
        // Another company's structures are not returned.
        Assert.DoesNotContain(response.Data, s => s.CompanyId == CompanyB);
    }

    [Fact]
    public async Task Search_structure_scope_returns_matches_across_the_whole_structure()
    {
        var f = Build();
        SeedDoc(f, Manuals, "Quality Manual SOP");
        SeedDoc(f, Forms, "Quality Form A");

        var response = await f.Explorer.SearchAsync(
            new ExplorerSearchInput(CompanyA, QualityRoot, null, ExplorerSearchScope.Structure, "quality", null, true, null), Corr, CancellationToken.None);

        Assert.True(response.IsSuccessful);
        var docs = response.Data!.Results.Where(r => r.ResultType == "DOCUMENT").ToList();
        Assert.Equal(2, docs.Count); // both folders searched under the structure
        Assert.All(docs, d => Assert.NotNull(d.Permissions));
        Assert.Contains(response.Data.Results, r => r.ResultType == "FOLDER"); // folder name match too
    }

    [Fact]
    public async Task Search_current_folder_scope_returns_only_that_folder()
    {
        var f = Build();
        SeedDoc(f, Manuals, "Manual One");
        SeedDoc(f, Forms, "Form One");

        var response = await f.Explorer.SearchAsync(
            new ExplorerSearchInput(CompanyA, QualityRoot, Manuals, ExplorerSearchScope.CurrentFolder, "one", null, true, null), Corr, CancellationToken.None);

        Assert.True(response.IsSuccessful);
        var docs = response.Data!.Results.Where(r => r.ResultType == "DOCUMENT").ToList();
        Assert.Single(docs);
        Assert.Equal("Manual One", docs[0].Name);
    }

    [Fact]
    public async Task Search_does_not_leak_documents_in_folders_without_view_grant()
    {
        // A CLAIMED principal (CompanyB) with a folder-view grant on Manuals only → Forms documents must not leak.
        // (A claimless token is a tenant-wide viewer under the FU04 Deny-only rollout; non-leakage is enforced for
        // properly-claimed users, which is what this test pins.)
        var userId = Guid.Parse("99999999-9999-9999-9999-999999999999");
        var f = Build(principalCompanies: [CompanyB], folderViewUserId: userId, grantedFolders: [QualityRoot, Manuals]);
        SeedDoc(f, Manuals, "Visible Doc");
        SeedDoc(f, Forms, "Hidden Doc");

        var response = await f.Explorer.SearchAsync(
            new ExplorerSearchInput(CompanyA, QualityRoot, null, ExplorerSearchScope.Structure, "doc", null, true, null), Corr, CancellationToken.None);

        Assert.True(response.IsSuccessful);
        var names = response.Data!.Results.Select(r => r.Name).ToList();
        Assert.Contains("Visible Doc", names);
        Assert.DoesNotContain("Hidden Doc", names);
    }

    [Fact]
    public async Task Copy_document_to_another_folder_creates_independent_copy_and_leaves_source()
    {
        var f = Build();
        var sourceId = SeedDoc(f, Manuals, "Spec");

        var response = await f.Documents.CopyAsync(sourceId, Forms, null, Corr, CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(201, response.StatusCode);
        Assert.Equal(Forms, response.Data!.CollectionInstanceId);
        Assert.Equal(sourceId, response.Data.CopiedFromDocumentId);
        Assert.Equal(2, f.DocumentRepo.Items.Count); // source + copy
        Assert.Contains(f.DocumentRepo.Items, d => d.Id == sourceId && d.CollectionInstanceId == Manuals); // source unchanged
    }

    [Fact]
    public async Task Copy_to_cross_company_folder_is_blocked()
    {
        var f = Build();
        var sourceId = SeedDoc(f, Manuals, "Spec");
        // A target folder owned by CompanyB.
        var foreign = Guid.Parse("cc000000-0000-0000-0000-0000000000ff");
        f.Reader.Items.Add(new CollectionInstanceReferenceDto(foreign, CompanyB, B1, "x", null, "Foreign", "Foreign", "ACTIVE", true, []));

        var response = await f.Documents.CopyAsync(sourceId, foreign, null, Corr, CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
        Assert.Equal(1, f.DocumentRepo.Items.Count); // no copy created
    }

    [Fact]
    public async Task Folder_documents_returns_current_users_favorite_state()
    {
        var f = Build();
        var documentId = SeedDoc(f, Manuals, "Favorite Spec");
        f.Favorites.Items.Add((f.CurrentUser.UserId, documentId));

        var response = await f.FolderDocuments.GetFolderDocumentsAsync(Manuals, Corr, CancellationToken.None);

        Assert.True(response.IsSuccessful);
        var document = Assert.Single(response.Data!.Documents);
        Assert.Equal(documentId, document.Id);
        Assert.True(document.IsFavorite);
    }

    [Theory]
    [InlineData(ControlledDocumentLifecycleStatus.Draft)]
    [InlineData(ControlledDocumentLifecycleStatus.InReview)]
    [InlineData(ControlledDocumentLifecycleStatus.Suspended)]
    [InlineData(ControlledDocumentLifecycleStatus.Superseded)]
    public async Task Ordinary_user_cannot_list_detail_or_download_non_effective_document(
        ControlledDocumentLifecycleStatus lifecycleStatus)
    {
        var f = Build(governanceAccess: false, grantReadAction: true);
        var documentId = SeedDoc(f, Manuals, lifecycleStatus.ToString(), lifecycleStatus);
        var versionId = f.VersionRepo.Items.Single(x => x.DocumentId == documentId).Id;

        var folder = await f.FolderDocuments.GetFolderDocumentsAsync(Manuals, true, Corr, CancellationToken.None);
        var detail = await f.Documents.GetDetailAsync(documentId, Corr, CancellationToken.None);
        var download = await f.Documents.DownloadAsync(documentId, versionId, Corr, CancellationToken.None);

        Assert.Empty(folder.Data!.Documents); // includeNonEffective is ignored without governance permission.
        Assert.Equal(404, detail.StatusCode);
        Assert.Equal(404, download.StatusCode);
    }

    [Fact]
    public async Task Ordinary_user_can_list_detail_and_download_effective_document()
    {
        var f = Build(governanceAccess: false, grantReadAction: true);
        var documentId = SeedDoc(f, Manuals, "Effective", ControlledDocumentLifecycleStatus.Effective);
        var versionId = f.VersionRepo.Items.Single(x => x.DocumentId == documentId).Id;

        var folder = await f.FolderDocuments.GetFolderDocumentsAsync(Manuals, Corr, CancellationToken.None);
        var detail = await f.Documents.GetDetailAsync(documentId, Corr, CancellationToken.None);
        var download = await f.Documents.DownloadAsync(documentId, versionId, Corr, CancellationToken.None);

        Assert.Contains(folder.Data!.Documents, x => x.Id == documentId && x.IsOfficiallyEffective);
        Assert.True(detail.IsSuccessful);
        Assert.True(download.IsSuccessful);
    }

    [Fact]
    public async Task Ordinary_user_cannot_consume_legacy_unlinked_document_but_governance_actor_can()
    {
        var ordinary = Build(governanceAccess: false, grantReadAction: true);
        var ordinaryDocumentId = SeedDoc(ordinary, Manuals, "Legacy", lifecycleStatus: null);
        var ordinaryVersionId = ordinary.VersionRepo.Items.Single().Id;

        Assert.Empty((await ordinary.FolderDocuments.GetFolderDocumentsAsync(Manuals, true, Corr, CancellationToken.None)).Data!.Documents);
        Assert.Equal(404, (await ordinary.Documents.GetDetailAsync(ordinaryDocumentId, Corr, CancellationToken.None)).StatusCode);
        Assert.Equal(404, (await ordinary.Documents.DownloadAsync(ordinaryDocumentId, ordinaryVersionId, Corr, CancellationToken.None)).StatusCode);

        var governance = Build(governanceAccess: true, grantReadAction: true);
        var governanceDocumentId = SeedDoc(governance, Manuals, "Legacy", lifecycleStatus: null);
        var governanceVersionId = governance.VersionRepo.Items.Single().Id;

        Assert.Contains(
            (await governance.FolderDocuments.GetFolderDocumentsAsync(Manuals, true, Corr, CancellationToken.None)).Data!.Documents,
            x => x.Id == governanceDocumentId && !x.IsOfficiallyEffective);
        Assert.True((await governance.Documents.GetDetailAsync(governanceDocumentId, Corr, CancellationToken.None)).IsSuccessful);
        Assert.True((await governance.Documents.DownloadAsync(governanceDocumentId, governanceVersionId, Corr, CancellationToken.None)).IsSuccessful);
    }

    [Theory]
    [InlineData(ControlledDocumentLifecycleStatus.Draft)]
    [InlineData(ControlledDocumentLifecycleStatus.InReview)]
    [InlineData(ControlledDocumentLifecycleStatus.Suspended)]
    [InlineData(ControlledDocumentLifecycleStatus.Superseded)]
    public async Task Governance_actor_can_list_non_effective_only_when_explicitly_requested(
        ControlledDocumentLifecycleStatus lifecycleStatus)
    {
        var f = Build(governanceAccess: true);
        var documentId = SeedDoc(f, Manuals, lifecycleStatus.ToString(), lifecycleStatus);

        var defaultFolder = await f.FolderDocuments.GetFolderDocumentsAsync(Manuals, false, Corr, CancellationToken.None);
        var inclusiveFolder = await f.FolderDocuments.GetFolderDocumentsAsync(Manuals, true, Corr, CancellationToken.None);
        var detail = await f.Documents.GetDetailAsync(documentId, Corr, CancellationToken.None);

        Assert.DoesNotContain(defaultFolder.Data!.Documents, x => x.Id == documentId);
        Assert.Contains(inclusiveFolder.Data!.Documents, x =>
            x.Id == documentId
            && x.MasterRegisterLifecycleStatus == lifecycleStatus.ToString()
            && !x.IsOfficiallyEffective);
        Assert.True(detail.IsSuccessful);
        Assert.True(detail.Data!.CanViewNonEffective);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static Guid SeedDoc(
        Fixture f,
        Guid folderId,
        string title,
        ControlledDocumentLifecycleStatus? lifecycleStatus = ControlledDocumentLifecycleStatus.Effective)
    {
        var id = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        f.DocumentRepo.Items.Add(new ControlledDocument
        {
            Id = id,
            TenantId = TenantId,
            DocumentKey = $"k-{id:N}",
            CompanyId = CompanyA,
            OwnerCompanyId = CompanyA,
            CollectionInstanceId = folderId,
            CollectionPath = "Quality",
            Title = title,
            DocumentType = DocumentType.Sop,
            CurrentVersionId = versionId,
            CurrentVersionNumber = 1,
            Status = ControlledItemStatus.Active,
            CreatedBy = "seed"
        });
        f.VersionRepo.Items.Add(new ControlledDocumentVersion
        {
            Id = versionId,
            TenantId = TenantId,
            DocumentId = id,
            VersionNumber = 1,
            FileRef = new ContentRef { ContentId = Guid.NewGuid(), StorageProvider = "fake", ObjectKey = "k", FileName = "f.pdf", MediaType = "application/pdf", Checksum = "x" },
            Checksum = "x",
            UploadedBy = "seed",
            VersionStatus = DocumentVersionStatus.Active,
            CreatedBy = "seed"
        });
        if (lifecycleStatus is { } status)
        {
            f.MasterRegister.Items.Add(new DocumentMasterRegisterEntry
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                ControlledDocumentId = id,
                DocumentTitle = title,
                LifecycleStatus = status
            });
        }
        return id;
    }

    private static Fixture Build(
        IReadOnlyCollection<Guid>? principalCompanies = null,
        Guid? folderViewUserId = null,
        IReadOnlyCollection<Guid>? grantedFolders = null,
        bool governanceAccess = true,
        bool grantReadAction = false)
    {
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(TenantId);

        var reader = new FakeCollectionInstanceReferenceReader();
        reader.Items.Add(new CollectionInstanceReferenceDto(QualityRoot, CompanyA, B1, "quality", null, "Quality", "Quality", "ACTIVE", true, []));
        reader.Items.Add(new CollectionInstanceReferenceDto(Manuals, CompanyA, B1, "manuals", "quality", "Manuals", "Quality/Manuals", "ACTIVE", true, []));
        reader.Items.Add(new CollectionInstanceReferenceDto(Forms, CompanyA, B1, "forms", "quality", "Forms", "Quality/Forms", "ACTIVE", true, []));
        reader.Items.Add(new CollectionInstanceReferenceDto(SafetyRoot, CompanyA, B2, "safety", null, "Safety", "Safety", "ACTIVE", true, []));

        var documentRepo = new FakeControlledDocumentRepository();
        var versionRepo = new FakeControlledDocumentVersionRepository();
        var templateRepo = new FakeTemplateDocumentRepository();
        var shares = new FakeDocumentShareRecordRepository();
        var folderPolicies = new FakeFolderDocumentAccessPolicyRepository();
        var favorites = new FakeDocumentFavoriteRepository();
        var storage = new FakeContentStorageGateway();
        var masterRegister = new FakeDocumentMasterRegisterRepository();

        if (folderViewUserId is { } uid)
        {
            foreach (var folderId in grantedFolders ?? [])
            {
                folderPolicies.Items.Add(new FolderDocumentAccessPolicy
                {
                    TenantId = TenantId,
                    CollectionInstanceId = folderId,
                    CompanyId = CompanyA,
                    TargetType = AccessTargetType.User,
                    TargetId = uid.ToString("D"),
                    FolderPermissions = new FolderPermissionSet { CanViewFolderDocuments = true, CanUploadDocument = true, CanEditFolderDocuments = true, CanUploadNewVersion = true, CanShareFolderDocuments = true, CanManageFolderDocumentAccess = true }
                });
            }
        }

        var permissions = governanceAccess ? [DocumentMasterRegisterPermissions.View] : Array.Empty<string>();
        var principal = folderViewUserId is { } u
            ? new DocumentPrincipal(u, [], principalCompanies ?? [], Permissions: permissions)
            : new DocumentPrincipal(Guid.NewGuid(), [], principalCompanies ?? [CompanyA], Permissions: permissions);
        if (grantReadAction)
        {
            folderPolicies.Items.Add(new FolderDocumentAccessPolicy
            {
                TenantId = TenantId,
                CollectionInstanceId = Manuals,
                CompanyId = CompanyA,
                TargetType = AccessTargetType.User,
                TargetId = principal.UserId.ToString("D"),
                FolderPermissions = new FolderPermissionSet { CanViewFolderDocuments = true }
            });
        }
        var access = new DocumentAccessEvaluator(
            folderPolicies,
            shares,
            new FakePrincipalAccessor(principal),
            masterRegister: masterRegister);
        var flags = Options.Create(new ControlledDocumentsFeatureFlagOptions());
        var versioning = new DocumentVersioningService(storage, tenantContext);
        var keyFactory = new DocumentKeyFactory();
        var currentUser = new FakeCurrentUserContext { UserId = principal.UserId };

        var explorer = new ControlledDocumentExplorerService(reader, documentRepo, templateRepo, access);
        var documents = new ControlledDocumentService(reader, documentRepo, versionRepo, shares, favorites, versioning, access, keyFactory, currentUser, tenantContext, flags);
        var folderDocuments = new FolderDocumentService(reader, documentRepo, templateRepo, favorites, folderPolicies, access, currentUser, tenantContext);

        return new Fixture(explorer, documents, folderDocuments, currentUser, favorites, reader, documentRepo, versionRepo, masterRegister);
    }

    private sealed record Fixture(
        ControlledDocumentExplorerService Explorer,
        ControlledDocumentService Documents,
        FolderDocumentService FolderDocuments,
        FakeCurrentUserContext CurrentUser,
        FakeDocumentFavoriteRepository Favorites,
        FakeCollectionInstanceReferenceReader Reader,
        FakeControlledDocumentRepository DocumentRepo,
        FakeControlledDocumentVersionRepository VersionRepo,
        FakeDocumentMasterRegisterRepository MasterRegister);
}
