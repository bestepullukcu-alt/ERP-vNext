using System.Reflection;
using System.Security.Claims;
using System.Text;
using Diten.Platform.API.Controllers;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.DocumentRepository;
using Diten.Platform.Application.Features.DocumentRepository;
using Diten.Platform.Application.Features.DocumentRepository.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentRepository;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Services.DocumentManagement;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Platform.Application.Tests.DocumentRepository;

/// <summary>
/// WP-VW-W3-PLT — the CrmVisitEvidence scope and its scope-bound <c>visit-evidence.*</c> keys (WP-VW-W3 §2.1).
/// <para>
/// Three things are pinned here, all against PRODUCTION code (the real local-filesystem gateway over a temp root,
/// the real <see cref="DocumentRepositoryService"/>, the real controller attributes and filter):
/// (1) the scope has its own <c>crm-visit-evidence</c> partition and the four existing keys are byte-identical;
/// (2) the scope's own file rule (images only, 10 MB, enforced while streaming, partial object removed);
/// (3) the authorisation matrix at the service boundary — a scope-bound key never opens another scope, a rep reads
/// and compensates only what they created (404 otherwise), read-all reads the tenant's evidence, and the general
/// read/manage keys behave exactly as before.
/// </para>
/// </summary>
public sealed class CrmVisitEvidenceScopeTests : IDisposable
{
    private const string Corr = "w3-plt-corr";
    private const string Rep = "rep.one@tenant.test";
    private const string OtherRep = "rep.two@tenant.test";
    private const string Manager = "manager@tenant.test";

    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-0000-0000-0000-00000000000a");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-0000-0000-0000-00000000000b");

    private readonly string _root = Path.Combine(Path.GetTempPath(), "DitenW3PltTests", Guid.NewGuid().ToString("N"));
    private readonly TenantAwareObjects _objects = new();

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); } catch { }
    }

    // ═════════════════════════ (1) scope → object key ═════════════════════════

    [Fact]
    public async Task CrmVisitEvidence_lands_in_its_own_partition_never_documents()
    {
        Guid tenant = Guid.NewGuid(), company = Guid.NewGuid(), item = Guid.NewGuid(), version = Guid.NewGuid();

        var stored = await Gateway().StoreAsync(new ContentStoreRequest(
            tenant, company, ContentStorageScope.CrmVisitEvidence, item, version, "photo.jpg", "image/jpeg",
            Png(64), Rep));

        Assert.True(stored.IsSuccessful, string.Join(";", stored.Errors ?? []));
        Assert.Equal(
            $"tenant-{tenant:D}/company-{company:D}/crm-visit-evidence/{item:D}/versions/{version:D}/photo.jpg",
            stored.Data!.ObjectKey);
        Assert.DoesNotContain("/documents/", stored.Data.ObjectKey);
    }

    [Fact]
    public void CrmVisitEvidence_is_value_4_and_projected_by_name()
    {
        Assert.Equal(4, (int)ContentStorageScope.CrmVisitEvidence);
        Assert.Equal("CrmVisitEvidence", ContentStorageScope.CrmVisitEvidence.ToString());
    }

    // The four existing scopes must keep byte-identical keys: persisted rows point at them.
    [Theory]
    [InlineData(ContentStorageScope.Documents, "documents")]
    [InlineData(ContentStorageScope.Templates, "templates")]
    [InlineData(ContentStorageScope.TaskAttachments, "task-attachments")]
    [InlineData(ContentStorageScope.ContentMessagingArtifacts, "content-messaging-artifacts")]
    public async Task Existing_scopes_keep_byte_identical_object_keys(ContentStorageScope scope, string segment)
    {
        Guid tenant = Guid.NewGuid(), company = Guid.NewGuid(), item = Guid.NewGuid(), version = Guid.NewGuid();

        var stored = await Gateway().StoreAsync(new ContentStoreRequest(
            tenant, company, scope, item, version, "file.pdf", "application/pdf", Bytes("payload"), "tester"));

        Assert.True(stored.IsSuccessful);
        Assert.Equal(
            $"tenant-{tenant:D}/company-{company:D}/{segment}/{item:D}/versions/{version:D}/file.pdf",
            stored.Data!.ObjectKey);
    }

    // ═════════════════════════ (2) the scope's own file rule ═════════════════════════

    [Fact]
    public async Task Pdf_is_rejected_in_the_evidence_scope_although_the_general_rule_allows_it()
    {
        var gateway = Gateway();

        var evidence = await gateway.StoreAsync(EvidenceRequest("report.pdf", "application/pdf", Bytes("%PDF")));
        var document = await gateway.StoreAsync(new ContentStoreRequest(
            TenantA, Guid.Empty, ContentStorageScope.Documents, Guid.NewGuid(), Guid.NewGuid(), "report.pdf",
            "application/pdf", Bytes("%PDF"), "tester"));

        Assert.False(evidence.IsSuccessful);
        Assert.Equal(400, evidence.StatusCode);
        Assert.Equal(DocumentRepositoryReasonCodes.ValidationFailed, evidence.ReasonCode);
        Assert.True(document.IsSuccessful); // other scopes stay on the general rule
    }

    [Theory]
    [InlineData("photo.png", "application/octet-stream")]
    [InlineData("photo.png", "application/pdf")]
    [InlineData("photo.gif", "image/gif")]
    [InlineData("photo.png", null)]
    public async Task Evidence_scope_rejects_a_wrong_type_or_media_type(string fileName, string? mediaType)
    {
        var result = await Gateway().StoreAsync(EvidenceRequest(fileName, mediaType, Png(32)));

        Assert.False(result.IsSuccessful);
        Assert.Equal(400, result.StatusCode);
    }

    [Theory]
    [InlineData("photo.jpg", "image/jpeg")]
    [InlineData("photo.jpeg", "image/jpeg")]
    [InlineData("signature.png", "image/png")]
    [InlineData("SIGNATURE.PNG", "IMAGE/PNG")]
    public async Task Evidence_scope_accepts_jpeg_and_png(string fileName, string mediaType)
    {
        var result = await Gateway().StoreAsync(EvidenceRequest(fileName, mediaType, Png(32)));

        Assert.True(result.IsSuccessful, string.Join(";", result.Errors ?? []));
    }

    // 11 MB → 400, the limit fires DURING the copy (the source is not read to the end) and nothing is left behind.
    [Fact]
    public async Task Eleven_megabytes_is_rejected_while_streaming_and_the_partial_object_is_removed()
    {
        var source = new CountingZeroStream(11 * 1024 * 1024);

        var result = await Gateway().StoreAsync(EvidenceRequest("photo.jpg", "image/jpeg", source));

        Assert.False(result.IsSuccessful);
        Assert.Equal(400, result.StatusCode);
        Assert.True(source.BytesRead <= CrmVisitEvidenceFileRule.DefaultMaxFileSizeBytes + 81_920,
            $"read {source.BytesRead} bytes — the limit must stop the copy, not check after it.");
        Assert.Empty(Directory.Exists(_root) ? Directory.GetFiles(_root, "*", SearchOption.AllDirectories) : []);
    }

    [Fact]
    public async Task Exactly_ten_megabytes_is_accepted()
    {
        var result = await Gateway().StoreAsync(EvidenceRequest(
            "photo.jpg", "image/jpeg", new CountingZeroStream(CrmVisitEvidenceFileRule.DefaultMaxFileSizeBytes)));

        Assert.True(result.IsSuccessful);
        Assert.Equal(CrmVisitEvidenceFileRule.DefaultMaxFileSizeBytes, result.Data!.ByteSize);
    }

    [Fact]
    public async Task Evidence_size_cap_comes_from_its_own_configuration_key_not_the_general_one()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DocumentManagement:ContentStorage:RootPath"] = _root,
            ["DocumentManagement:ContentStorage:MaxFileSizeBytes"] = "268435456",
            ["DocumentManagement:ContentStorage:Scopes:CrmVisitEvidence:MaxFileSizeBytes"] = "100"
        }).Build();
        var options = configuration.GetSection(ContentStorageOptions.SectionName).Get<ContentStorageOptions>()!;
        var gateway = new LocalFileSystemContentStorageGateway(
            Options.Create(options), NullLogger<LocalFileSystemContentStorageGateway>.Instance);

        Assert.Equal(100, options.Scopes.CrmVisitEvidence.MaxFileSizeBytes);
        Assert.False((await gateway.StoreAsync(EvidenceRequest("p.jpg", "image/jpeg", Png(101)))).IsSuccessful);
        Assert.True((await gateway.StoreAsync(EvidenceRequest("p.jpg", "image/jpeg", Png(100)))).IsSuccessful);
    }

    [Fact]
    public void Unconfigured_environment_gets_the_ten_megabyte_default()
    {
        var options = new ConfigurationBuilder().Build()
            .GetSection(ContentStorageOptions.SectionName).Get<ContentStorageOptions>() ?? new ContentStorageOptions();

        Assert.Equal(10_485_760, options.Scopes.CrmVisitEvidence.MaxFileSizeBytes);
        Assert.Null(options.Scopes.For(ContentStorageScope.Documents));
        Assert.Null(options.Scopes.For(ContentStorageScope.TaskAttachments));
        Assert.Null(options.Scopes.For(ContentStorageScope.ContentMessagingArtifacts));
    }

    // ═════════════════════════ (3) authorisation matrix (service boundary) ═════════════════════════

    [Fact]
    public async Task Scoped_writer_cannot_write_to_documents_403_and_nothing_is_stored()
    {
        var rep = Service(TenantA, Rep, DocumentRepositoryPermissions.VisitEvidenceWrite);

        var result = await rep.StoreForCallerAsync(Input(ContentStorageScope.Documents, "file.png"), Corr, default);

        Assert.False(result.IsSuccessful);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal(DocumentRepositoryReasonCodes.Forbidden, result.ReasonCode);
        Assert.Empty(_objects.Rows);
        Assert.Empty(Directory.Exists(_root) ? Directory.GetFiles(_root, "*", SearchOption.AllDirectories) : []);
    }

    [Theory]
    [InlineData(ContentStorageScope.Templates)]
    [InlineData(ContentStorageScope.TaskAttachments)]
    [InlineData(ContentStorageScope.ContentMessagingArtifacts)]
    public async Task Scoped_writer_cannot_write_to_any_other_scope(ContentStorageScope scope)
    {
        var rep = Service(TenantA, Rep, DocumentRepositoryPermissions.VisitEvidenceWrite);

        var result = await rep.StoreForCallerAsync(Input(scope, "file.png"), Corr, default);

        Assert.Equal(403, result.StatusCode);
    }

    [Fact]
    public async Task Scoped_writer_stores_evidence_and_becomes_its_creator()
    {
        var rep = Service(TenantA, Rep, DocumentRepositoryPermissions.VisitEvidenceWrite);

        var result = await rep.StoreForCallerAsync(Input(ContentStorageScope.CrmVisitEvidence), Corr, default);

        Assert.True(result.IsSuccessful, string.Join(";", result.Errors ?? []));
        Assert.Equal("CrmVisitEvidence", result.Data!.Scope);
        Assert.Equal(Rep, result.Data.CreatedBy);
        Assert.Equal(TenantA, _objects.Rows.Single().TenantId);
    }

    [Fact]
    public async Task Scoped_reader_reads_own_evidence_metadata_and_content()
    {
        var contentId = await StoreEvidenceAs(TenantA, Rep);
        var rep = Service(TenantA, Rep, DocumentRepositoryPermissions.VisitEvidenceRead);

        var meta = await rep.GetForCallerAsync(contentId, Corr, default);
        var content = await rep.OpenReadForCallerAsync(contentId, Corr, default);

        Assert.True(meta.IsSuccessful);
        Assert.True(content.IsSuccessful);
        await content.Data!.Content.DisposeAsync();
    }

    [Fact]
    public async Task Scoped_reader_gets_404_for_another_reps_evidence()
    {
        var contentId = await StoreEvidenceAs(TenantA, OtherRep);
        var rep = Service(TenantA, Rep, DocumentRepositoryPermissions.VisitEvidenceRead);

        var meta = await rep.GetForCallerAsync(contentId, Corr, default);
        var content = await rep.OpenReadForCallerAsync(contentId, Corr, default);

        AssertNotFound(meta);
        AssertNotFound(content);
    }

    [Fact]
    public async Task Scoped_reader_gets_404_for_an_object_of_another_scope_even_if_they_created_it()
    {
        var contentId = await StoreAs(TenantA, Rep, ContentStorageScope.Documents, DocumentRepositoryPermissions.Manage);
        var rep = Service(TenantA, Rep,
            DocumentRepositoryPermissions.VisitEvidenceRead, DocumentRepositoryPermissions.VisitEvidenceReadAll);

        AssertNotFound(await rep.GetForCallerAsync(contentId, Corr, default));
        AssertNotFound(await rep.OpenReadForCallerAsync(contentId, Corr, default));
    }

    [Fact]
    public async Task Read_all_reads_every_reps_evidence_in_the_tenant()
    {
        var first = await StoreEvidenceAs(TenantA, Rep);
        var second = await StoreEvidenceAs(TenantA, OtherRep);
        var manager = Service(TenantA, Manager, DocumentRepositoryPermissions.VisitEvidenceReadAll);

        Assert.True((await manager.GetForCallerAsync(first, Corr, default)).IsSuccessful);
        Assert.True((await manager.GetForCallerAsync(second, Corr, default)).IsSuccessful);
        var content = await manager.OpenReadForCallerAsync(second, Corr, default);
        Assert.True(content.IsSuccessful);
        await content.Data!.Content.DisposeAsync();
    }

    [Fact]
    public async Task General_read_is_unchanged_it_reads_documents_and_evidence_alike()
    {
        var document = await StoreAs(TenantA, OtherRep, ContentStorageScope.Documents, DocumentRepositoryPermissions.Manage);
        var evidence = await StoreEvidenceAs(TenantA, OtherRep);
        var reader = Service(TenantA, Manager, DocumentRepositoryPermissions.Read);

        Assert.True((await reader.GetForCallerAsync(document, Corr, default)).IsSuccessful);
        Assert.True((await reader.GetForCallerAsync(evidence, Corr, default)).IsSuccessful);
    }

    [Fact]
    public async Task General_manage_is_unchanged_it_writes_any_scope_and_compensates_anyones_object()
    {
        var admin = Service(TenantA, Manager, DocumentRepositoryPermissions.Manage);

        Assert.True((await admin.StoreForCallerAsync(Input(ContentStorageScope.Documents, "file.png"), Corr, default)).IsSuccessful);
        var evidence = await StoreEvidenceAs(TenantA, Rep);

        var compensated = await admin.CompensateForCallerAsync(evidence, Corr, default);

        Assert.True(compensated.IsSuccessful);
        Assert.Contains(evidence, _objects.Compensated);
    }

    [Fact]
    public async Task Compensation_with_the_scoped_key_is_owner_only()
    {
        var own = await StoreEvidenceAs(TenantA, Rep);
        var others = await StoreEvidenceAs(TenantA, OtherRep);
        var rep = Service(TenantA, Rep, DocumentRepositoryPermissions.VisitEvidenceWrite);

        var refused = await rep.CompensateForCallerAsync(others, Corr, default);
        var allowed = await rep.CompensateForCallerAsync(own, Corr, default);

        AssertNotFound(refused);
        Assert.DoesNotContain(others, _objects.Compensated);
        Assert.True(allowed.IsSuccessful);
        Assert.Equal(204, allowed.StatusCode);
        Assert.Contains(own, _objects.Compensated);
    }

    [Fact]
    public async Task Scoped_writer_cannot_compensate_their_own_object_of_another_scope()
    {
        var document = await StoreAs(TenantA, Rep, ContentStorageScope.Documents, DocumentRepositoryPermissions.Manage);
        var rep = Service(TenantA, Rep, DocumentRepositoryPermissions.VisitEvidenceWrite);

        AssertNotFound(await rep.CompensateForCallerAsync(document, Corr, default));
        Assert.Empty(_objects.Compensated);
    }

    [Fact]
    public async Task Reader_without_write_cannot_compensate_even_own_evidence()
    {
        var own = await StoreEvidenceAs(TenantA, Rep);
        var rep = Service(TenantA, Rep,
            DocumentRepositoryPermissions.VisitEvidenceRead, DocumentRepositoryPermissions.VisitEvidenceReadAll);

        AssertNotFound(await rep.CompensateForCallerAsync(own, Corr, default));
    }

    [Fact]
    public async Task Cross_tenant_stays_404_for_every_key_including_general_read_and_read_all()
    {
        var evidence = await StoreEvidenceAs(TenantA, Rep);
        var otherTenant = Service(TenantB, Rep,
            DocumentRepositoryPermissions.Read, DocumentRepositoryPermissions.Manage,
            DocumentRepositoryPermissions.VisitEvidenceRead, DocumentRepositoryPermissions.VisitEvidenceReadAll,
            DocumentRepositoryPermissions.VisitEvidenceWrite);

        AssertNotFound(await otherTenant.GetForCallerAsync(evidence, Corr, default));
        AssertNotFound(await otherTenant.OpenReadForCallerAsync(evidence, Corr, default));
        AssertNotFound(await otherTenant.CompensateForCallerAsync(evidence, Corr, default));
    }

    [Fact]
    public async Task Without_a_permission_context_every_caller_decision_is_a_denial()
    {
        var evidence = await StoreEvidenceAs(TenantA, Rep);
        var service = new DocumentRepositoryService(
            Gateway(), _objects, Tenant(TenantA), new User(Rep), NullLogger<DocumentRepositoryService>.Instance);

        Assert.Equal(403, (await service.StoreForCallerAsync(Input(ContentStorageScope.CrmVisitEvidence), Corr, default)).StatusCode);
        AssertNotFound(await service.GetForCallerAsync(evidence, Corr, default));
        AssertNotFound(await service.CompensateForCallerAsync(evidence, Corr, default));
    }

    [Fact]
    public async Task The_anonymous_system_actor_never_owns_anything()
    {
        var evidence = await StoreEvidenceAs(TenantA, "system");
        var anonymous = Service(TenantA, "system",
            DocumentRepositoryPermissions.VisitEvidenceRead, DocumentRepositoryPermissions.VisitEvidenceWrite);

        AssertNotFound(await anonymous.GetForCallerAsync(evidence, Corr, default));
        AssertNotFound(await anonymous.CompensateForCallerAsync(evidence, Corr, default));
    }

    // ═════════════════════════ controller door + catalogue ═════════════════════════

    [Theory]
    [InlineData(nameof(DocumentRepositoryController.Store), DocumentRepositoryPermissions.Manage, new[] { DocumentRepositoryPermissions.VisitEvidenceWrite })]
    [InlineData(nameof(DocumentRepositoryController.Compensate), DocumentRepositoryPermissions.Manage, new[] { DocumentRepositoryPermissions.VisitEvidenceWrite })]
    [InlineData(nameof(DocumentRepositoryController.Get), DocumentRepositoryPermissions.Read, new[] { DocumentRepositoryPermissions.VisitEvidenceRead, DocumentRepositoryPermissions.VisitEvidenceReadAll })]
    [InlineData(nameof(DocumentRepositoryController.Content), DocumentRepositoryPermissions.Read, new[] { DocumentRepositoryPermissions.VisitEvidenceRead, DocumentRepositoryPermissions.VisitEvidenceReadAll })]
    public void Each_action_admits_the_general_key_or_its_scope_bound_keys(string action, string general, string[] scoped)
    {
        var attributes = typeof(DocumentRepositoryController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Single(m => m.Name == action)
            .GetCustomAttributes<HasPermissionAttribute>().ToList();

        var attribute = Assert.Single(attributes);
        Assert.Equal(general, attribute.Permission);
        Assert.Equal(scoped.OrderBy(k => k), attribute.AlternativePermissions.OrderBy(k => k));
    }

    [Fact]
    public async Task Or_attribute_admits_the_alternative_key_and_still_denies_an_unrelated_one()
    {
        var attribute = new HasPermissionAttribute(
            DocumentRepositoryPermissions.Manage, DocumentRepositoryPermissions.VisitEvidenceWrite);

        Assert.True(await AdmitsAsync(attribute, DocumentRepositoryPermissions.VisitEvidenceWrite));
        Assert.True(await AdmitsAsync(attribute, DocumentRepositoryPermissions.Manage));
        Assert.False(await AdmitsAsync(attribute, DocumentRepositoryPermissions.VisitEvidenceRead));
        Assert.False(await AdmitsAsync(attribute));
    }

    [Fact]
    public void Auto_registration_reflects_the_three_visit_evidence_keys()
    {
        var keys = HasPermissionReflector.CollectPermissionKeys(typeof(HasPermissionAttribute).Assembly);

        Assert.Contains(DocumentRepositoryPermissions.VisitEvidenceWrite, keys);
        Assert.Contains(DocumentRepositoryPermissions.VisitEvidenceRead, keys);
        Assert.Contains(DocumentRepositoryPermissions.VisitEvidenceReadAll, keys);
        Assert.Contains(DocumentRepositoryPermissions.Read, keys);
        Assert.Contains(DocumentRepositoryPermissions.Manage, keys);
    }

    // ═════════════════════════ helpers ═════════════════════════

    private LocalFileSystemContentStorageGateway Gateway() => new(
        Options.Create(new ContentStorageOptions
        {
            RootPath = _root,
            MaxFileSizeBytes = 268_435_456,
            AllowedExtensions = [".pdf", ".png", ".jpg", ".jpeg"],
            AllowedMediaTypes = []
        }),
        NullLogger<LocalFileSystemContentStorageGateway>.Instance);

    private DocumentRepositoryService Service(Guid tenantId, string actor, params string[] permissions) => new(
        Gateway(), _objects, Tenant(tenantId), new User(actor), NullLogger<DocumentRepositoryService>.Instance,
        new Permissions(permissions));

    private ITenantContext Tenant(Guid tenantId)
    {
        _objects.CurrentTenant = tenantId;
        return new TenantStub(tenantId);
    }

    private Task<Guid> StoreEvidenceAs(Guid tenantId, string actor) =>
        StoreAs(tenantId, actor, ContentStorageScope.CrmVisitEvidence, DocumentRepositoryPermissions.VisitEvidenceWrite);

    private async Task<Guid> StoreAs(Guid tenantId, string actor, ContentStorageScope scope, string key)
    {
        var stored = await Service(tenantId, actor, key).StoreForCallerAsync(Input(scope), Corr, default);
        Assert.True(stored.IsSuccessful, string.Join(";", stored.Errors ?? []));
        return stored.Data!.ContentId;
    }

    private static StoreRepositoryObjectInput Input(ContentStorageScope scope, string fileName = "photo.png") =>
        new(scope, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), fileName, "image/png", Png(48));

    private static ContentStoreRequest EvidenceRequest(string fileName, string? mediaType, Stream content) =>
        new(TenantA, Guid.Empty, ContentStorageScope.CrmVisitEvidence, Guid.NewGuid(), Guid.NewGuid(),
            fileName, mediaType, content, Rep);

    private static MemoryStream Bytes(string s) => new(Encoding.UTF8.GetBytes(s), writable: false);

    private static MemoryStream Png(int size)
    {
        var bytes = new byte[size];
        new Random(size).NextBytes(bytes);
        return new MemoryStream(bytes, writable: false);
    }

    private static void AssertNotFound<T>(Response<T> response)
    {
        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
        Assert.Equal(DocumentRepositoryReasonCodes.NotFoundNonLeakage, response.ReasonCode);
    }

    private static async Task<bool> AdmitsAsync(HasPermissionAttribute attribute, params string[] claims)
    {
        var identity = new ClaimsIdentity(
            claims.Select(c => new Claim("permission", c)).Prepend(new Claim("actor_type", "tenant_user")), "Test");
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity),
            RequestServices = new ServiceCollection().BuildServiceProvider()
        };
        var filterContext = new AuthorizationFilterContext(
            new ActionContext(httpContext, new RouteData(), new ActionDescriptor()), new List<IFilterMetadata>());

        await attribute.OnAuthorizationAsync(filterContext);
        return filterContext.Result is null;
    }

    /// <summary>A repository that, like the Mongo one, only ever sees the current tenant's rows — so another
    /// tenant's object resolves to <c>null</c> and the service answers 404.</summary>
    private sealed class TenantAwareObjects : IRepositoryObjectRepository
    {
        public Guid CurrentTenant { get; set; }
        public List<RepositoryObject> Rows { get; } = [];
        public List<Guid> Compensated { get; } = [];

        public Task<RepositoryObject> CreateAsync(RepositoryObject repositoryObject, CancellationToken ct = default)
        {
            Rows.Add(repositoryObject);
            return Task.FromResult(repositoryObject);
        }

        public Task<RepositoryObject?> GetByContentIdAsync(Guid contentId, CancellationToken ct = default) =>
            Task.FromResult(Rows.FirstOrDefault(r => r.ContentId == contentId && r.TenantId == CurrentTenant));

        public Task<IReadOnlyList<RepositoryObject>> GetByOwningItemAsync(Guid owningItemId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<RepositoryObject>>(
                Rows.Where(r => r.OwningItemId == owningItemId && r.TenantId == CurrentTenant).ToList());

        public Task<bool> MarkCompensatedAsync(Guid contentId, CancellationToken ct = default)
        {
            Compensated.Add(contentId);
            return Task.FromResult(true);
        }
    }

    private sealed class Permissions(IEnumerable<string> keys) : IActorPermissionContext
    {
        private readonly HashSet<string> _keys = new(keys, StringComparer.Ordinal);
        public bool IsPlatformActor => false;
        public bool Has(string? permissionKey) => permissionKey is not null && _keys.Contains(permissionKey);
    }

    private sealed class User(string actor) : ICurrentUserContext
    {
        public Guid UserId => Guid.Empty;
        public string? Email => actor;
        public string? DisplayName => actor;
        public string ActorName => actor;
        public bool IsAuthenticated => true;
    }

    private sealed class TenantStub(Guid tenantId) : ITenantContext
    {
        public Guid TenantId { get; private set; } = tenantId;
        public bool IsResolved => true;
        public bool IsPlatformContext => false;
        public Guid? TargetTenantId => null;
        public void SetTenant(Guid id) => TenantId = id;
        public void SetPlatformContext(Guid targetTenantId) { }
        public void ClearTenant() { }
    }

    /// <summary>A non-seekable stream of zeros that counts how much was actually pulled from it, so a test can
    /// tell a limit enforced during the copy from one checked after the whole body was read.</summary>
    private sealed class CountingZeroStream(long length) : Stream
    {
        public long BytesRead { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => BytesRead; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count)
        {
            var n = (int)Math.Min(count, length - BytesRead);
            if (n <= 0) return 0;
            Array.Clear(buffer, offset, n);
            BytesRead += n;
            return n;
        }
    }
}
