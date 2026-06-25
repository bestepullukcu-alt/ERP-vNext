using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Services;
using Diten.Platform.Application.Features.TenantOrganization.Services;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Tests.DocumentManagement;

// MOD-0029-FU01 — in-memory fakes for the controlled-document service tests (no Mongo).

internal sealed class FakeContentStorageGateway : IContentStorageGateway
{
    public bool FailStore { get; set; }
    public List<ContentStoreResult> Stored { get; } = [];
    public List<string> Deleted { get; } = [];

    public Task<Response<ContentStoreResult>> StoreAsync(ContentStoreRequest request, CancellationToken ct = default)
    {
        if (FailStore)
        {
            return Task.FromResult(Response<ContentStoreResult>.Fail("Content storage is unavailable.", 503, "STORAGE_UNAVAILABLE"));
        }

        var result = new ContentStoreResult(
            Guid.NewGuid(),
            "fake",
            $"tenant/{request.ItemId:N}/{request.VersionId:N}/{request.FileName}",
            request.FileName,
            request.DeclaredMediaType ?? "application/octet-stream",
            request.Content.LongLength,
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(request.Content)).ToLowerInvariant());
        Stored.Add(result);
        return Task.FromResult(Response<ContentStoreResult>.Success(result));
    }

    public Task<Response<ContentStreamResult>> OpenReadAsync(string storageProvider, string objectKey, CancellationToken ct = default)
    {
        var bytes = new byte[] { 1, 2, 3 };
        return Task.FromResult(Response<ContentStreamResult>.Success(
            new ContentStreamResult(new MemoryStream(bytes), "application/octet-stream", "file", bytes.Length)));
    }

    public Task<bool> TryDeleteAsync(string storageProvider, string objectKey, CancellationToken ct = default)
    {
        Deleted.Add(objectKey);
        return Task.FromResult(true);
    }
}

internal sealed class FakeCollectionInstanceReferenceReader : ICollectionInstanceReferenceReader
{
    public List<CollectionInstanceReferenceDto> Items { get; } = [];

    public Task<CollectionInstanceReferenceDto?> ResolveByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Items.FirstOrDefault(x => x.CollectionInstanceId == id));

    public Task<bool> ValidateScopeAsync(Guid id, Guid companyId, CancellationToken ct = default) =>
        Task.FromResult(Items.Any(x => x.CollectionInstanceId == id && x.CompanyId == companyId));

    public Task<CollectionPathSnapshot?> GetPathSnapshotAsync(Guid id, CancellationToken ct = default)
    {
        var item = Items.FirstOrDefault(x => x.CollectionInstanceId == id);
        return Task.FromResult(item is null ? null : new CollectionPathSnapshot(item.CollectionInstanceId, item.CompanyId, item.CanonicalId, item.FullPath));
    }

    public Task<CollectionInstanceCompanyBinding?> GetCompanyBindingAsync(Guid id, CancellationToken ct = default)
    {
        var item = Items.FirstOrDefault(x => x.CollectionInstanceId == id);
        return Task.FromResult(item is null ? null : new CollectionInstanceCompanyBinding(item.CompanyId, item.ScopeBindings));
    }

    public Task<bool> IsUsableAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Items.Any(x => x.CollectionInstanceId == id && x.IsUsable));

    public Task<IReadOnlyList<CollectionInstanceReferenceDto>> GetBranchAsync(Guid rootId, CancellationToken ct = default)
    {
        var root = Items.FirstOrDefault(x => x.CollectionInstanceId == rootId);
        if (root is null)
        {
            return Task.FromResult<IReadOnlyList<CollectionInstanceReferenceDto>>([]);
        }

        var prefix = root.FullPath + "/";
        var branch = Items
            .Where(x => x.CompanyId == root.CompanyId && (x.CollectionInstanceId == rootId || x.FullPath == root.FullPath || x.FullPath.StartsWith(prefix, StringComparison.Ordinal)))
            .ToList();
        return Task.FromResult<IReadOnlyList<CollectionInstanceReferenceDto>>(branch);
    }
}

internal sealed class FakePrincipalAccessor : IDocumentAccessPrincipalAccessor
{
    private readonly DocumentPrincipal _principal;
    public FakePrincipalAccessor(DocumentPrincipal principal) => _principal = principal;
    public DocumentPrincipal GetPrincipal() => _principal;
}

internal sealed class FakeCurrentUserContext : ICurrentUserContext
{
    public Guid UserId { get; init; } = Guid.Parse("99999999-9999-9999-9999-999999999999");
    public string? Email => "fu01@example.test";
    public string? DisplayName => "FU01 Tester";
    public string ActorName => "fu01@example.test";
    public bool IsAuthenticated => true;
}

internal sealed class FakeLegalEntityReferenceValidator : ILegalEntityReferenceValidator
{
    private readonly bool _referenceable;
    public FakeLegalEntityReferenceValidator(bool referenceable) => _referenceable = referenceable;
    public Task<Response<LegalEntityReferenceDto>> ValidateAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_referenceable
            ? Response<LegalEntityReferenceDto>.Success(new LegalEntityReferenceDto(id, "Co", "Co", "ACTIVE", true))
            : Response<LegalEntityReferenceDto>.Fail("not referenceable", 404));
}

internal sealed class FakeControlledDocumentRepository : IControlledDocumentRepository
{
    public List<ControlledDocument> Items { get; } = [];
    public bool FailCreate { get; set; }
    public Task<ControlledDocument> CreateAsync(ControlledDocument d, CancellationToken ct = default)
    {
        if (FailCreate) throw new InvalidOperationException("create failed");
        Items.Add(d); return Task.FromResult(d);
    }
    public Task<ControlledDocument?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(x => x.Id == id && !x.IsDeleted));
    public Task<ControlledDocument?> GetByDocumentKeyAsync(string key, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(x => x.DocumentKey == key && !x.IsDeleted));
    public Task<IReadOnlyList<ControlledDocument>> GetAllForTenantAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ControlledDocument>>(Items.Where(x => !x.IsDeleted).ToList());
    public Task<IReadOnlyList<ControlledDocument>> GetByCompanyAsync(Guid companyId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ControlledDocument>>(Items.Where(x => x.OwnerCompanyId == companyId && !x.IsDeleted).ToList());
    public Task<IReadOnlyList<ControlledDocument>> GetByCollectionInstanceAsync(Guid id, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ControlledDocument>>(Items.Where(x => x.CollectionInstanceId == id && !x.IsDeleted).ToList());
    public Task<bool> UpdateAsync(ControlledDocument d, CancellationToken ct = default) { var i = Items.FindIndex(x => x.Id == d.Id); if (i >= 0) Items[i] = d; return Task.FromResult(i >= 0); }
}

internal sealed class FakeControlledDocumentVersionRepository : IControlledDocumentVersionRepository
{
    public List<ControlledDocumentVersion> Items { get; } = [];
    public bool FailCreate { get; set; }
    public Task<ControlledDocumentVersion> CreateAsync(ControlledDocumentVersion v, CancellationToken ct = default)
    {
        if (FailCreate) throw new InvalidOperationException("version create failed");
        if (Items.Any(x => x.DocumentId == v.DocumentId && x.VersionNumber == v.VersionNumber)) throw new InvalidOperationException("duplicate version");
        Items.Add(v); return Task.FromResult(v);
    }
    public Task<ControlledDocumentVersion?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(x => x.Id == id && !x.IsDeleted));
    public Task<IReadOnlyList<ControlledDocumentVersion>> GetByDocumentAsync(Guid docId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ControlledDocumentVersion>>(Items.Where(x => x.DocumentId == docId && !x.IsDeleted).ToList());
    public Task<ControlledDocumentVersion?> GetByDocumentAndNumberAsync(Guid docId, int n, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(x => x.DocumentId == docId && x.VersionNumber == n));
    public Task<int> GetMaxVersionNumberAsync(Guid docId, CancellationToken ct = default) => Task.FromResult(Items.Where(x => x.DocumentId == docId).Select(x => x.VersionNumber).DefaultIfEmpty(0).Max());
    public Task SupersedeActiveVersionsAsync(Guid docId, Guid except, CancellationToken ct = default) { foreach (var v in Items.Where(x => x.DocumentId == docId && x.Id != except && x.VersionStatus == DocumentVersionStatus.Active)) v.VersionStatus = DocumentVersionStatus.Superseded; return Task.CompletedTask; }
    public Task DeleteAsync(Guid id, CancellationToken ct = default) { Items.RemoveAll(x => x.Id == id); return Task.CompletedTask; }
}

internal sealed class FakeTemplateDocumentRepository : ITemplateDocumentRepository
{
    public List<TemplateDocument> Items { get; } = [];
    public Task<TemplateDocument> CreateAsync(TemplateDocument t, CancellationToken ct = default) { Items.Add(t); return Task.FromResult(t); }
    public Task<TemplateDocument?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(x => x.Id == id && !x.IsDeleted));
    public Task<TemplateDocument?> GetByTemplateKeyAsync(string key, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(x => x.TemplateKey == key && !x.IsDeleted));
    public Task<IReadOnlyList<TemplateDocument>> GetAllForTenantAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<TemplateDocument>>(Items.Where(x => !x.IsDeleted).ToList());
    public Task<IReadOnlyList<TemplateDocument>> GetByCompanyAsync(Guid companyId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<TemplateDocument>>(Items.Where(x => x.OwnerCompanyId == companyId && !x.IsDeleted).ToList());
    public Task<IReadOnlyList<TemplateDocument>> GetByCollectionInstanceAsync(Guid id, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<TemplateDocument>>(Items.Where(x => x.CollectionInstanceId == id && !x.IsDeleted).ToList());
    public Task<bool> UpdateAsync(TemplateDocument t, CancellationToken ct = default) { var i = Items.FindIndex(x => x.Id == t.Id); if (i >= 0) Items[i] = t; return Task.FromResult(i >= 0); }
}

internal sealed class FakeTemplateVersionRepository : ITemplateVersionRepository
{
    public List<TemplateVersion> Items { get; } = [];
    public Task<TemplateVersion> CreateAsync(TemplateVersion v, CancellationToken ct = default)
    {
        if (Items.Any(x => x.TemplateId == v.TemplateId && x.VersionNumber == v.VersionNumber)) throw new InvalidOperationException("duplicate version");
        Items.Add(v); return Task.FromResult(v);
    }
    public Task<TemplateVersion?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(x => x.Id == id && !x.IsDeleted));
    public Task<IReadOnlyList<TemplateVersion>> GetByTemplateAsync(Guid tId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<TemplateVersion>>(Items.Where(x => x.TemplateId == tId && !x.IsDeleted).ToList());
    public Task<TemplateVersion?> GetByTemplateAndNumberAsync(Guid tId, int n, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(x => x.TemplateId == tId && x.VersionNumber == n));
    public Task<int> GetMaxVersionNumberAsync(Guid tId, CancellationToken ct = default) => Task.FromResult(Items.Where(x => x.TemplateId == tId).Select(x => x.VersionNumber).DefaultIfEmpty(0).Max());
    public Task SupersedeActiveVersionsAsync(Guid tId, Guid except, CancellationToken ct = default) { foreach (var v in Items.Where(x => x.TemplateId == tId && x.Id != except && x.VersionStatus == DocumentVersionStatus.Active)) v.VersionStatus = DocumentVersionStatus.Superseded; return Task.CompletedTask; }
    public Task DeleteAsync(Guid id, CancellationToken ct = default) { Items.RemoveAll(x => x.Id == id); return Task.CompletedTask; }
}

internal sealed class FakeFolderDocumentAccessPolicyRepository : IFolderDocumentAccessPolicyRepository
{
    public List<FolderDocumentAccessPolicy> Items { get; } = [];
    public Task<FolderDocumentAccessPolicy> UpsertAsync(FolderDocumentAccessPolicy p, CancellationToken ct = default)
    {
        Items.RemoveAll(x => x.CollectionInstanceId == p.CollectionInstanceId && x.TargetType == p.TargetType && x.TargetId == p.TargetId);
        Items.Add(p); return Task.FromResult(p);
    }
    public Task<IReadOnlyList<FolderDocumentAccessPolicy>> GetByCollectionInstanceAsync(Guid id, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<FolderDocumentAccessPolicy>>(Items.Where(x => x.CollectionInstanceId == id && !x.IsDeleted).ToList());
    public Task<IReadOnlyList<FolderDocumentAccessPolicy>> GetByCollectionInstanceAndTargetsAsync(Guid id, IReadOnlyList<(AccessTargetType, string)> targets, CancellationToken ct = default)
    {
        var set = targets.ToHashSet();
        return Task.FromResult<IReadOnlyList<FolderDocumentAccessPolicy>>(Items.Where(x => x.CollectionInstanceId == id && set.Contains((x.TargetType, x.TargetId))).ToList());
    }
}

internal sealed class FakeDocumentShareRecordRepository : IDocumentShareRecordRepository
{
    public List<DocumentShareRecord> Items { get; } = [];
    public Task<DocumentShareRecord> CreateAsync(DocumentShareRecord s, CancellationToken ct = default) { Items.Add(s); return Task.FromResult(s); }
    public Task<IReadOnlyList<DocumentShareRecord>> CreateManyAsync(IReadOnlyList<DocumentShareRecord> s, CancellationToken ct = default) { Items.AddRange(s); return Task.FromResult(s); }
    public Task<IReadOnlyList<DocumentShareRecord>> GetByItemAsync(SharedItemKind kind, Guid itemId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<DocumentShareRecord>>(Items.Where(x => x.ItemKind == kind && x.ItemId == itemId && !x.IsDeleted).ToList());
    public Task<IReadOnlyList<DocumentShareRecord>> GetSharesForTargetCompanyAsync(Guid companyId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<DocumentShareRecord>>(Items.Where(x => x.TargetCompanyId == companyId && !x.IsDeleted).ToList());
    public Task<bool> ExistsAsync(SharedItemKind kind, Guid itemId, Guid companyId, CancellationToken ct = default) => Task.FromResult(Items.Any(x => x.ItemKind == kind && x.ItemId == itemId && x.TargetCompanyId == companyId && !x.IsDeleted));
}

internal sealed class FakeFolderShareOperationRepository : IFolderShareOperationRepository
{
    public List<FolderShareOperation> Items { get; } = [];
    public Task<FolderShareOperation> CreateAsync(FolderShareOperation o, CancellationToken ct = default) { Items.Add(o); return Task.FromResult(o); }
    public Task<FolderShareOperation?> GetByOperationIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(x => x.OperationId == id && !x.IsDeleted));
}

internal sealed class FakeFolderShareOutcomeRepository : IFolderShareOutcomeRepository
{
    public List<FolderShareOutcome> Items { get; } = [];
    public Task<IReadOnlyList<FolderShareOutcome>> CreateManyAsync(IReadOnlyList<FolderShareOutcome> o, CancellationToken ct = default) { Items.AddRange(o); return Task.FromResult(o); }
    public Task<IReadOnlyList<FolderShareOutcome>> GetByOperationIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<FolderShareOutcome>>(Items.Where(x => x.OperationId == id && !x.IsDeleted).ToList());
}
