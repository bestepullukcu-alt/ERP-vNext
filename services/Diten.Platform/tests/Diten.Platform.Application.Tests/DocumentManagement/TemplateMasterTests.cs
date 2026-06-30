using Diten.Platform.Application.Features.DocumentManagementControlledDocuments;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Services;
using Diten.Platform.Application.Features.DocumentManagementTemplateMasters;
using Diten.Platform.Application.Features.DocumentManagementTemplateMasters.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using Xunit;

namespace Diten.Platform.Application.Tests.DocumentManagement;

public sealed class TemplateMasterTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid OwnerCompanyId = Guid.Parse("aaaaaaaa-1111-2222-3333-444444444444");
    private const string Corr = "fu02-corr-1";

    [Fact]
    public async Task Create_template_master_normalizes_code_and_persists_metadata()
    {
        var f = Fixture();

        var response = await f.Service.CreateAsync(CreateInput() with { MasterCode = " qms-sop-001 " }, Corr, CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(201, response.StatusCode);
        var master = Assert.Single(f.Masters.Items);
        Assert.Equal("QMS-SOP-001", master.MasterCode);
        Assert.Equal("SOP", master.Classification);
        Assert.Equal(TemplateVariantPolicy.Allowed, master.VariantPolicy);
        Assert.Null(master.CurrentVersionId);
        Assert.Equal(0, master.CurrentMasterVersion);
    }

    [Fact]
    public async Task Duplicate_master_code_is_rejected()
    {
        var f = Fixture();
        await f.Service.CreateAsync(CreateInput(), Corr, CancellationToken.None);

        var response = await f.Service.CreateAsync(CreateInput() with { TemplateName = "Duplicate" }, Corr, CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Equal(TemplateMasterReasonCodes.DuplicateMasterCode, response.ReasonCode);
        Assert.Single(f.Masters.Items);
    }

    [Fact]
    public async Task Publish_new_master_version_uses_storage_and_updates_current_version()
    {
        var f = Fixture();
        var created = await f.Service.CreateAsync(CreateInput(), Corr, CancellationToken.None);

        var response = await f.Service.PublishVersionAsync(created.Data!.Id, File("v1"), "initial", false, Corr, CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(201, response.StatusCode);
        Assert.Single(f.Storage.Stored);
        var version = Assert.Single(f.Versions.Items);
        Assert.Equal(1, version.VersionNumber);
        Assert.Equal(TemplateMasterVersionStatus.Published, version.Status);
        Assert.Equal(version.Id, f.Masters.Items.Single().CurrentVersionId);
        Assert.Equal(TemplateMasterStatus.Published, f.Masters.Items.Single().Status);
    }

    [Fact]
    public async Task Publish_identical_content_is_rejected_without_second_storage_write()
    {
        var f = Fixture();
        var created = await f.Service.CreateAsync(CreateInput(), Corr, CancellationToken.None);
        await f.Service.PublishVersionAsync(created.Data!.Id, File("v1"), "initial", false, Corr, CancellationToken.None);

        var response = await f.Service.PublishVersionAsync(created.Data!.Id, File("v1"), "same", false, Corr, CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Equal(TemplateMasterReasonCodes.NoContentChange, response.ReasonCode);
        Assert.Single(f.Storage.Stored);
        Assert.Single(f.Versions.Items);
    }

    [Fact]
    public async Task Deprecate_published_master_sets_lifecycle_fields()
    {
        var f = Fixture();
        var created = await f.Service.CreateAsync(CreateInput(), Corr, CancellationToken.None);
        await f.Service.PublishVersionAsync(created.Data!.Id, File("v1"), "initial", false, Corr, CancellationToken.None);

        var response = await f.Service.DeprecateAsync(created.Data!.Id, new DeprecateTemplateMasterInput("replace"), Corr, CancellationToken.None);

        Assert.True(response.IsSuccessful);
        var master = f.Masters.Items.Single();
        Assert.Equal(TemplateMasterStatus.Deprecated, master.Status);
        Assert.NotNull(master.DeprecatedAt);
        Assert.Equal("fu01@example.test", master.DeprecatedBy);
    }

    [Fact]
    public async Task Adoption_impact_returns_basic_lineage_count_only()
    {
        var f = Fixture();
        var created = await f.Service.CreateAsync(CreateInput(), Corr, CancellationToken.None);
        f.TemplateDocuments.Items.Add(new TemplateDocument
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            TemplateKey = "TMP-1",
            CompanyId = OwnerCompanyId,
            OwnerCompanyId = OwnerCompanyId,
            Title = "Folder Template",
            TemplateMasterId = created.Data!.Id
        });

        var response = await f.Service.GetAdoptionImpactAsync(created.Data!.Id, Corr, CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(1, response.Data!.FolderAttachedTemplateCount);
        Assert.Equal(0, response.Data.VariantCount);
    }

    [Fact]
    public async Task Delete_soft_deletes_master_and_hides_it()
    {
        var f = Fixture();
        var created = await f.Service.CreateAsync(CreateInput(), Corr, CancellationToken.None);

        var response = await f.Service.DeleteAsync(created.Data!.Id, Corr, CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.True(f.Masters.Items.Single().IsDeleted);
        var refetch = await f.Service.GetDetailAsync(created.Data!.Id, Corr, CancellationToken.None);
        Assert.False(refetch.IsSuccessful);
        Assert.Equal(404, refetch.StatusCode);
    }

    [Fact]
    public async Task Delete_missing_master_returns_not_found_non_leakage()
    {
        var f = Fixture();

        var response = await f.Service.DeleteAsync(Guid.NewGuid(), Corr, CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
        Assert.Equal(TemplateMasterReasonCodes.NotFoundNonLeakage, response.ReasonCode);
    }

    [Fact]
    public async Task Bulk_delete_soft_deletes_only_matching_masters()
    {
        var f = Fixture();
        var a = await f.Service.CreateAsync(CreateInput(), Corr, CancellationToken.None);
        var b = await f.Service.CreateAsync(CreateInput() with { MasterCode = "QMS-SOP-002" }, Corr, CancellationToken.None);
        await f.Service.CreateAsync(CreateInput() with { MasterCode = "QMS-SOP-003" }, Corr, CancellationToken.None);

        var response = await f.Service.BulkDeleteAsync([a.Data!.Id, b.Data!.Id], Corr, CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(2, response.Data);
        Assert.Equal(2, f.Masters.Items.Count(x => x.IsDeleted));
        Assert.Equal(1, f.Masters.Items.Count(x => !x.IsDeleted));
    }

    [Fact]
    public async Task Bulk_delete_with_no_ids_is_rejected()
    {
        var f = Fixture();

        var response = await f.Service.BulkDeleteAsync([], Corr, CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Equal(TemplateMasterReasonCodes.ValidationFailed, response.ReasonCode);
    }

    private static Harness Fixture()
    {
        var tenant = new TenantContext();
        tenant.SetTenant(TenantId);
        var storage = new FakeContentStorageGateway();
        var versioning = new DocumentVersioningService(storage, tenant);
        var masters = new FakeTemplateMasterRepository();
        var versions = new FakeTemplateMasterVersionRepository();
        var templateDocuments = new FakeTemplateDocumentRepository();
        var service = new TemplateMasterService(
            masters,
            versions,
            templateDocuments,
            versioning,
            tenant,
            new FakeCurrentUserContext());

        return new Harness(service, masters, versions, templateDocuments, storage);
    }

    private static CreateTemplateMasterInput CreateInput() => new(
        "QMS-SOP-001",
        "QMS SOP Master",
        "Reusable SOP master",
        "SOP",
        null,
        null,
        "ALLOWED",
        OwnerCompanyId,
        null,
        null);

    private static FileUploadInput File(string content) =>
        new("master.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(content)));

    private sealed record Harness(
        TemplateMasterService Service,
        FakeTemplateMasterRepository Masters,
        FakeTemplateMasterVersionRepository Versions,
        FakeTemplateDocumentRepository TemplateDocuments,
        FakeContentStorageGateway Storage);

    private sealed class FakeTemplateMasterRepository : ITemplateMasterRepository
    {
        public List<TemplateMaster> Items { get; } = [];

        public Task<TemplateMaster> CreateAsync(TemplateMaster master, CancellationToken ct = default)
        {
            if (Items.Any(x => x.MasterCode == master.MasterCode && !x.IsDeleted))
            {
                throw new InvalidOperationException("duplicate");
            }

            Items.Add(master);
            return Task.FromResult(master);
        }

        public Task<TemplateMaster?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => x.Id == id && !x.IsDeleted));

        public Task<TemplateMaster?> GetByMasterCodeAsync(string masterCode, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => x.MasterCode == masterCode && !x.IsDeleted));

        public Task<IReadOnlyList<TemplateMaster>> ListAsync(string? status, string? classification, Guid? collectionDefinitionId, string? canonicalId, string? variantPolicy, CancellationToken ct = default)
        {
            IEnumerable<TemplateMaster> query = Items.Where(x => !x.IsDeleted);
            if (Enum.TryParse<TemplateMasterStatus>(status, true, out var parsedStatus)) query = query.Where(x => x.Status == parsedStatus);
            if (!string.IsNullOrWhiteSpace(classification)) query = query.Where(x => x.Classification == classification);
            if (collectionDefinitionId.HasValue) query = query.Where(x => x.CollectionDefinitionId == collectionDefinitionId);
            if (!string.IsNullOrWhiteSpace(canonicalId)) query = query.Where(x => x.CanonicalId == canonicalId);
            if (Enum.TryParse<TemplateVariantPolicy>(variantPolicy, true, out var parsedPolicy)) query = query.Where(x => x.VariantPolicy == parsedPolicy);
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

        public Task<TemplateMasterVersion> CreateAsync(TemplateMasterVersion version, CancellationToken ct = default)
        {
            if (Items.Any(x => x.TemplateMasterId == version.TemplateMasterId && x.VersionNumber == version.VersionNumber && !x.IsDeleted))
            {
                throw new InvalidOperationException("duplicate version");
            }

            Items.Add(version);
            return Task.FromResult(version);
        }

        public Task<TemplateMasterVersion?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => x.Id == id && !x.IsDeleted));

        public Task<IReadOnlyList<TemplateMasterVersion>> GetByMasterAsync(Guid templateMasterId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TemplateMasterVersion>>(Items.Where(x => x.TemplateMasterId == templateMasterId && !x.IsDeleted).OrderByDescending(x => x.VersionNumber).ToList());

        public Task<TemplateMasterVersion?> GetByMasterAndNumberAsync(Guid templateMasterId, int versionNumber, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => x.TemplateMasterId == templateMasterId && x.VersionNumber == versionNumber && !x.IsDeleted));

        public Task<int> GetMaxVersionNumberAsync(Guid templateMasterId, CancellationToken ct = default) =>
            Task.FromResult(Items.Where(x => x.TemplateMasterId == templateMasterId && !x.IsDeleted).Select(x => x.VersionNumber).DefaultIfEmpty(0).Max());

        public Task SupersedePublishedVersionsAsync(Guid templateMasterId, Guid exceptVersionId, CancellationToken ct = default)
        {
            foreach (var version in Items.Where(x => x.TemplateMasterId == templateMasterId && x.Id != exceptVersionId && x.Status == TemplateMasterVersionStatus.Published))
            {
                version.Status = TemplateMasterVersionStatus.Superseded;
            }

            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            var version = Items.FirstOrDefault(x => x.Id == id);
            if (version is not null) version.IsDeleted = true;
            return Task.CompletedTask;
        }
    }
}
