using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.Platform.API.Controllers.Internal;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Contracts.Behaviors;
using Diten.Platform.Application.Contracts.DocumentRepository;
using Diten.Platform.Application.Features.DocumentRepository;
using Diten.Platform.Application.Features.DocumentRepository.Internal;
using Diten.Platform.Application.Features.DocumentRepository.Services;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Entities.DocumentRepository;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Services.DocumentManagement;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Platform.Application.Tests.DocumentRepository;

/// <summary>
/// WP-VW-W6 PLT-W6-DOC — <c>api/internal/document-repository</c> over real HTTP. Real: the controller (key check, user-token
/// refusal), MVC's binder, <see cref="InternalDocumentRepositoryService"/>, the audited
/// <see cref="StoreInternalRepositoryObjectCommand"/> run through the real <see cref="AuditBehavior{TRequest,TResponse}"/>,
/// <see cref="DocumentRepositoryService"/>, the real <see cref="LocalFileSystemContentStorageGateway"/> over a temp root
/// (no real storage), and the real <see cref="TenantContext"/>. Doubled: the tenant registry, the repository rows (they read
/// the AMBIENT tenant, as the Mongo execution filter does — an unpinned read throws, as the real context does) and the audit
/// sink (it records what would be appended and in which tenant).
/// </summary>
public sealed class InternalDocumentRepositoryTests : IDisposable
{
    private const string ApiKey = "test-internal-key-plt-w6-doc";
    private const string Crm = "MOD-0168";
    private static readonly Guid TenantA = Guid.Parse("a1a1a1a1-0000-0000-0000-0000000000d6");
    private static readonly Guid TenantB = Guid.Parse("b2b2b2b2-0000-0000-0000-0000000000d6");
    private static readonly Guid Suspended = Guid.Parse("c3c3c3c3-0000-0000-0000-0000000000d6");
    private static readonly Guid Order = Guid.Parse("0d0d0d0d-0000-0000-0000-0000000000d6");

    private readonly string _root = Path.Combine(Path.GetTempPath(), "DitenPltW6DocTests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); } catch { }
    }

    // ── the door: the key first, no user token ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("wrong-key")]
    public async Task Without_the_internal_key_both_doors_answer_401_and_nothing_is_written(string? key)
    {
        using var host = new Host(_root);

        var write = await host.PostAsync(Body(), key);
        var read = await host.GetAsync(Guid.NewGuid(), TenantA, Crm, key);

        Assert.Equal(HttpStatusCode.Unauthorized, write.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, read.StatusCode);
        Assert.Empty(host.Rows.All);
        Assert.Empty(host.Audit.Requests);
    }

    [Fact]
    public async Task A_call_that_carries_a_user_token_is_refused_even_with_the_key()
    {
        using var host = new Host(_root);

        var write = await host.PostAsync(Body(), ApiKey, bearer: "eyJ.user.token");
        var read = await host.GetAsync(Guid.NewGuid(), TenantA, Crm, ApiKey, bearer: "eyJ.user.token");

        Assert.Equal(HttpStatusCode.Unauthorized, write.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, read.StatusCode);
        Assert.Empty(host.Rows.All);
    }

    // ── write → read ───────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_pdf_is_stored_in_its_own_partition_answered_by_ref_size_and_sha256_and_read_back_byte_for_byte()
    {
        using var host = new Host(_root);
        var pdf = Pdf(4096);

        var stored = await host.StoredAsync(await host.PostAsync(Body(content: pdf)));

        Assert.Equal(pdf.Length, stored.SizeBytes);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(pdf)).ToLowerInvariant(), stored.Sha256);
        var row = Assert.Single(host.Rows.All);
        Assert.Equal((TenantA, stored.DocumentRef, Order, "CommercialPreOrderDocument"), (row.TenantId, row.ContentId, row.OwningItemId, row.Scope));
        Assert.Contains("/commercial-pre-order-documents/", row.ObjectKey);
        Assert.StartsWith($"tenant-{TenantA:D}/", row.ObjectKey);

        var read = await host.GetAsync(stored.DocumentRef, TenantA, Crm);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal("application/pdf", read.Content.Headers.ContentType!.MediaType);
        var bytes = await read.Content.ReadAsByteArrayAsync();
        Assert.Equal(stored.Sha256, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
    }

    [Fact]
    public async Task The_write_is_audited_in_the_bodys_tenant_with_the_asking_module_and_the_system_as_actor()
    {
        using var host = new Host(_root);

        var stored = await host.StoredAsync(await host.PostAsync(Body()));

        var audit = Assert.Single(host.Audit.Requests);
        Assert.Equal(nameof(StoreInternalRepositoryObjectCommand), audit.RequestType);
        Assert.Equal((AuditCategory.DocumentManagement, AuditOperation.Create, "RepositoryObject", AuditOutcome.Succeeded),
            (audit.Category, audit.Operation, audit.EntityType, audit.Outcome));
        Assert.Equal((Crm, (Guid?)TenantA, (Guid?)Order), (audit.SourceModule, audit.TargetTenantId, audit.EntityId));
        Assert.Equal(AuditActorType.System, audit.ActorType);
        // The audit service resolves the record's tenant from the ambient context: it is the body's, pinned.
        Assert.Equal(TenantA, Assert.Single(host.Audit.AmbientTenants));
        Assert.Equal("CommercialPreOrderDocument", audit.Metadata["scope"]);
        Assert.NotEqual(Guid.Empty, stored.DocumentRef);
    }

    // ── the (module, scope) pairs ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(Crm, "CrmVisitEvidence")]
    [InlineData(Crm, "Documents")]
    [InlineData(Crm, "Templates")]
    [InlineData("CRM", "TaskAttachments")]
    [InlineData("MOD-0001", "CommercialPreOrderDocument")]
    [InlineData("HR", "CommercialPreOrderDocument")]
    public async Task A_module_writes_only_the_scope_it_is_allowed_and_a_refusal_writes_nothing(string module, string scope)
    {
        using var host = new Host(_root);

        var response = await host.PostAsync(Body(module: module, scope: scope));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(host.Rows.All);
        Assert.Empty(Directory.Exists(_root) ? Directory.GetFiles(_root, "*", SearchOption.AllDirectories) : []);
    }

    [Theory]
    [InlineData("6")]
    [InlineData("commercialpreorderdocument")]
    [InlineData("NoSuchScope")]
    public async Task A_scope_is_named_exactly_never_by_number_or_another_spelling(string scope)
    {
        using var host = new Host(_root);
        Assert.Equal(HttpStatusCode.BadRequest, (await host.PostAsync(Body(scope: scope))).StatusCode);
        Assert.Empty(host.Rows.All);
    }

    [Fact]
    public async Task Crm_reads_its_two_scopes_and_nothing_else()
    {
        using var host = new Host(_root);
        var signature = host.Rows.Seed(TenantA, "CrmVisitEvidence", host.Gateway, "signature.png", Encoding.ASCII.GetBytes("png-bytes"));
        var document = host.Rows.Seed(TenantA, "Documents", host.Gateway, "contract.pdf", Pdf(64));
        var task = host.Rows.Seed(TenantA, "TaskAttachments", host.Gateway, "a.pdf", Pdf(64));

        Assert.Equal(HttpStatusCode.OK, (await host.GetAsync(signature, TenantA, Crm)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await host.GetAsync(document, TenantA, Crm)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await host.GetAsync(task, TenantA, Crm)).StatusCode);
        // An unknown module reads nothing, not even the CRM's scopes.
        Assert.Equal(HttpStatusCode.NotFound, (await host.GetAsync(signature, TenantA, "HR")).StatusCode);
    }

    // ── the tenant ─────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Another_tenants_document_reads_as_not_found()
    {
        using var host = new Host(_root);
        var stored = await host.StoredAsync(await host.PostAsync(Body()));

        Assert.Equal(HttpStatusCode.NotFound, (await host.GetAsync(stored.DocumentRef, TenantB, Crm)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.GetAsync(stored.DocumentRef, TenantA, Crm)).StatusCode);
    }

    [Fact]
    public async Task An_unknown_or_inactive_tenant_is_404_for_both_doors_and_nothing_is_written()
    {
        using var host = new Host(_root);
        var stored = await host.StoredAsync(await host.PostAsync(Body()));

        Assert.Equal(HttpStatusCode.NotFound, (await host.PostAsync(Body(tenant: Guid.NewGuid()))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await host.PostAsync(Body(tenant: Suspended))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await host.GetAsync(stored.DocumentRef, Suspended, Crm)).StatusCode);
        Assert.Single(host.Rows.All);
    }

    // ── the file ───────────────────────────────────────────────────────────────────────────────────────────

    public static IEnumerable<object[]> BadFiles()
    {
        yield return ["text-plain", (Func<object>)(() => Body(contentType: "text/plain"))];
        yield return ["not-pdf-bytes", (Func<object>)(() => Body(content: Encoding.ASCII.GetBytes("hello, not a pdf at all")))];
        yield return ["not-pdf-name", (Func<object>)(() => Body(fileName: "depot.txt"))];
        yield return ["over-256kb", (Func<object>)(() => Body(content: Pdf(256 * 1024 + 1)))];
        yield return ["bad-base64", (Func<object>)(() => Body() with { ContentBase64 = "%%%not-base64%%%" })];
        yield return ["empty", (Func<object>)(() => Body() with { ContentBase64 = "" })];
        yield return ["path-name", (Func<object>)(() => Body(fileName: "../depot.pdf"))];
        // FU-PLT-DOC-FILENAME-TEST (W6-BE-c2) — a folder or a drive in the name, without "..": the /, \ and : rule alone refuses them
        yield return ["slash-name", (Func<object>)(() => Body(fileName: "dir/depot.pdf"))];
        yield return ["backslash-name", (Func<object>)(() => Body(fileName: @"dir\depot.pdf"))];
        yield return ["drive-name", (Func<object>)(() => Body(fileName: "c:depot.pdf"))];
        yield return ["no-owner", (Func<object>)(() => Body() with { OwnerItemId = Guid.Empty })];
    }

    [Theory]
    [MemberData(nameof(BadFiles))]
    public async Task A_file_that_is_not_one_pdf_of_at_most_256_kb_is_400_and_nothing_is_written(string shape, Func<object> body)
    {
        using var host = new Host(_root);

        var response = await host.PostAsync((InternalDocumentStoreRequest)body());

        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, $"{shape}: {(int)response.StatusCode}");
        Assert.Empty(host.Rows.All);
    }

    [Fact]
    public async Task Exactly_256_kb_is_accepted()
    {
        using var host = new Host(_root);
        var stored = await host.StoredAsync(await host.PostAsync(Body(content: Pdf(256 * 1024))));
        Assert.Equal(256 * 1024, stored.SizeBytes);
    }

    [Fact]
    public async Task The_scopes_own_rule_holds_in_the_gateway_too_not_only_at_the_door()
    {
        // The in-process path (no door): the gateway itself refuses a non-PDF and an oversized file in this scope.
        var gateway = Gateway(_root);
        var tooBig = await gateway.StoreAsync(new ContentStoreRequest(TenantA, Guid.Empty, ContentStorageScope.CommercialPreOrderDocument,
            Order, Guid.NewGuid(), "big.pdf", "application/pdf", new MemoryStream(Pdf(256 * 1024 + 10)), "system"));
        var png = await gateway.StoreAsync(new ContentStoreRequest(TenantA, Guid.Empty, ContentStorageScope.CommercialPreOrderDocument,
            Order, Guid.NewGuid(), "x.png", "image/png", new MemoryStream([1, 2, 3]), "system"));

        Assert.Equal(400, tooBig.StatusCode);
        Assert.Equal(400, png.StatusCode);
    }

    // ── the user path is unchanged ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_scope_bound_visit_evidence_keys_never_open_the_new_scope_and_the_general_keys_mean_what_they_did()
    {
        IActorPermissionContext Keys(params string[] keys) => new Permissions(keys);
        var row = new RepositoryObject
        {
            TenantId = TenantA, ContentId = Guid.NewGuid(), StorageProvider = "local", ObjectKey = "k", FileName = "d.pdf", MediaType = "application/pdf",
            Checksum = "x", Scope = nameof(ContentStorageScope.CommercialPreOrderDocument), CreatedBy = "rep@t.test"
        };

        Assert.False(DocumentRepositoryAccessPolicy.CanWrite(Keys(DocumentRepositoryPermissions.VisitEvidenceWrite), ContentStorageScope.CommercialPreOrderDocument));
        Assert.False(DocumentRepositoryAccessPolicy.CanRead(Keys(DocumentRepositoryPermissions.VisitEvidenceReadAll), row, "rep@t.test"));
        Assert.False(DocumentRepositoryAccessPolicy.CanRead(Keys(DocumentRepositoryPermissions.VisitEvidenceRead), row, "rep@t.test"));
        Assert.True(DocumentRepositoryAccessPolicy.CanWrite(Keys(DocumentRepositoryPermissions.VisitEvidenceWrite), ContentStorageScope.CrmVisitEvidence));
        Assert.True(DocumentRepositoryAccessPolicy.CanWrite(Keys(DocumentRepositoryPermissions.Manage), ContentStorageScope.Documents));
        // The scope values that existed keep their numbers; the new one is appended.
        Assert.Equal((5, 6), ((int)ContentStorageScope.CrmVisitEvidence, (int)ContentStorageScope.CommercialPreOrderDocument));
        // The visit evidence rule is untouched: images only, 10 MB.
        IContentStorageFileRule evidence = new ContentStorageOptions().Scopes.CrmVisitEvidence;
        Assert.Equal(10_485_760L, evidence.MaxFileSizeBytes);
        Assert.Equal([".jpg", ".jpeg", ".png"], evidence.AllowedExtensions);
        Assert.Equal(["image/jpeg", "image/png"], evidence.AllowedMediaTypes);
    }

    [Fact]
    public void The_new_scopes_size_is_configurable_and_its_types_are_not()
    {
        var options = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DocumentManagement:ContentStorage:Scopes:CommercialPreOrderDocument:MaxFileSizeBytes"] = "131072"
        }).Build().GetSection(ContentStorageOptions.SectionName).Get<ContentStorageOptions>()!;

        IContentStorageFileRule rule = options.Scopes.CommercialPreOrderDocument;
        Assert.Equal(131072, rule.MaxFileSizeBytes);
        Assert.Equal([".pdf"], rule.AllowedExtensions);
        Assert.Equal(["application/pdf"], rule.AllowedMediaTypes);
        Assert.Same(rule, options.Scopes.For(ContentStorageScope.CommercialPreOrderDocument));
    }

    // ── the world ──────────────────────────────────────────────────────────────────────────────────────────

    private static InternalDocumentStoreRequest Body(
        Guid? tenant = null, string module = Crm, string scope = "CommercialPreOrderDocument", string fileName = "MG-ORD-000001-depot.pdf",
        string contentType = "application/pdf", byte[]? content = null) =>
        new(tenant ?? TenantA, module, scope, Order, fileName, contentType, Convert.ToBase64String(content ?? Pdf(1024)));

    /// <summary>A byte run that starts like a PDF (the door checks the magic, not the structure).</summary>
    private static byte[] Pdf(int size)
    {
        var bytes = new byte[size];
        "%PDF-1.7\n"u8.CopyTo(bytes);
        for (var i = 9; i < size; i++)
        {
            bytes[i] = (byte)('a' + (i % 26));
        }

        return bytes;
    }

    private static LocalFileSystemContentStorageGateway Gateway(string root) =>
        new(Options.Create(new ContentStorageOptions { RootPath = root }), NullLogger<LocalFileSystemContentStorageGateway>.Instance);

    private sealed class Host : IDisposable
    {
        private readonly TestServer _server;

        public Rows Rows { get; } = new();
        public AuditSink Audit { get; } = new();
        public LocalFileSystemContentStorageGateway Gateway { get; }

        public Host(string root)
        {
            Gateway = InternalDocumentRepositoryTests.Gateway(root);
            _server = new TestServer(new WebHostBuilder()
                .UseEnvironment("Test")
                .ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AuthService:InternalApiKey"] = ApiKey
                }))
                .ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddLogging();
                    services.AddScoped<ITenantContext, TenantContext>();
                    services.AddSingleton<ITenantRegistryRepository>(new Tenants());
                    services.AddSingleton(Rows);
                    services.AddScoped<IRepositoryObjectRepository>(sp => Rows.For(sp.GetRequiredService<ITenantContext>()));
                    services.AddSingleton<IContentStorageGateway>(Gateway);
                    services.AddSingleton(Options.Create(new ContentStorageOptions { RootPath = root }));
                    services.AddSingleton<ICurrentUserContext>(new SystemActor());
                    services.AddSingleton(Audit);
                    services.AddScoped<DocumentRepositoryService>(sp => new DocumentRepositoryService(
                        sp.GetRequiredService<IContentStorageGateway>(), sp.GetRequiredService<IRepositoryObjectRepository>(),
                        sp.GetRequiredService<ITenantContext>(), sp.GetRequiredService<ICurrentUserContext>(),
                        NullLogger<DocumentRepositoryService>.Instance));
                    services.AddScoped<IMediator>(sp => new AuditingMediator(
                        new StoreInternalRepositoryObjectHandler(sp.GetRequiredService<DocumentRepositoryService>()),
                        new AuditBehavior<StoreInternalRepositoryObjectCommand, Response<RepositoryObjectModel>>(
                            Audit.For(sp.GetRequiredService<ITenantContext>()), new AnonymousTenantAuthorizationContext(), new AuditBehaviorOptions(),
                            NullLogger<AuditBehavior<StoreInternalRepositoryObjectCommand, Response<RepositoryObjectModel>>>.Instance)));
                    services.AddScoped<IInternalDocumentRepositoryService, InternalDocumentRepositoryService>();
                    services.AddControllers()
                        .ConfigureApplicationPartManager(manager =>
                        {
                            manager.ApplicationParts.Clear();
                            manager.ApplicationParts.Add(new AssemblyPart(typeof(InternalDocumentRepositoryController).Assembly));
                            manager.FeatureProviders.Add(new OnlyController(typeof(InternalDocumentRepositoryController)));
                        });
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                }));
        }

        public Task<HttpResponseMessage> PostAsync(InternalDocumentStoreRequest body, string? key = ApiKey, string? bearer = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/internal/document-repository/objects") { Content = JsonContent.Create(body) };
            Headers(request, key, bearer);
            return _server.CreateClient().SendAsync(request);
        }

        public Task<HttpResponseMessage> GetAsync(Guid documentRef, Guid tenant, string module, string? key = ApiKey, string? bearer = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Get,
                $"/api/internal/document-repository/objects/{documentRef:D}?tenantId={tenant:D}&sourceModule={Uri.EscapeDataString(module)}");
            Headers(request, key, bearer);
            return _server.CreateClient().SendAsync(request);
        }

        public async Task<InternalDocumentStoredDto> StoredAsync(HttpResponseMessage response)
        {
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(response.StatusCode == HttpStatusCode.Created, $"{(int)response.StatusCode}: {body}");
            using var json = JsonDocument.Parse(body);
            return json.RootElement.GetProperty("data").Deserialize<InternalDocumentStoredDto>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        }

        private static void Headers(HttpRequestMessage request, string? key, string? bearer)
        {
            if (key is not null)
            {
                request.Headers.TryAddWithoutValidation("X-Internal-Api-Key", key);
            }

            if (bearer is not null)
            {
                request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + bearer);
            }
        }

        public void Dispose() => _server.Dispose();
    }

    private sealed class OnlyController(Type controller) : ControllerFeatureProvider
    {
        protected override bool IsController(System.Reflection.TypeInfo typeInfo) => typeInfo.AsType() == controller;
    }

    /// <summary>The store command through the real audit behaviour, then the real handler; nothing else is routed.</summary>
    private sealed class AuditingMediator(
        StoreInternalRepositoryObjectHandler handler,
        AuditBehavior<StoreInternalRepositoryObjectCommand, Response<RepositoryObjectModel>> audit) : IMediator
    {
        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default) =>
            request is StoreInternalRepositoryObjectCommand command
                ? (TResponse)(object)await audit.Handle(command, () => handler.Handle(command, cancellationToken), cancellationToken)
                : throw new NotSupportedException(request.GetType().Name);

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => throw new NotSupportedException();
    }

    /// <summary>What would be appended, and the AMBIENT tenant at that moment (the real audit service owns the record by it).</summary>
    internal sealed class AuditSink
    {
        public List<AuditAppendRequest> Requests { get; } = [];
        public List<Guid> AmbientTenants { get; } = [];

        public IAuditService For(ITenantContext tenant) => new Scoped(this, tenant);

        private sealed class Scoped(AuditSink owner, ITenantContext tenant) : IAuditService
        {
            public Task<AuditAppendResult> AppendAsync(AuditAppendRequest request, CancellationToken ct = default)
            {
                owner.Requests.Add(request);
                owner.AmbientTenants.Add(tenant.IsResolved ? tenant.TenantId : Guid.Empty);
                return Task.FromResult(AuditAppendResult.Queued("audit:test"));
            }
        }
    }

    /// <summary>The repository rows; each read and write is in the AMBIENT tenant (the execution filter). An unresolved
    /// context throws, exactly as <see cref="TenantContext.TenantId"/> does.</summary>
    internal sealed class Rows
    {
        private readonly List<RepositoryObject> _rows = [];

        public IReadOnlyList<RepositoryObject> All
        {
            get { lock (_rows) { return _rows.ToList(); } }
        }

        public Guid Seed(Guid tenant, string scope, IContentStorageGateway gateway, string fileName, byte[] content)
        {
            var stored = gateway.StoreAsync(new ContentStoreRequest(tenant, Guid.Empty,
                Enum.Parse<ContentStorageScope>(scope), Order, Guid.NewGuid(), fileName,
                fileName.EndsWith(".png", StringComparison.Ordinal) ? "image/png" : "application/pdf", new MemoryStream(content), "seed")).GetAwaiter().GetResult();
            Assert.True(stored.IsSuccessful, string.Join("; ", stored.Errors));
            var row = new RepositoryObject
            {
                TenantId = tenant, ContentId = stored.Data!.ContentId, StorageProvider = stored.Data.StorageProvider, ObjectKey = stored.Data.ObjectKey,
                Scope = scope, OwningItemId = Order, FileName = fileName, MediaType = stored.Data.MediaType, ByteSize = stored.Data.ByteSize,
                Checksum = stored.Data.Checksum, CreatedBy = "seed"
            };
            lock (_rows) { _rows.Add(row); }
            return row.ContentId;
        }

        public IRepositoryObjectRepository For(ITenantContext tenant) => new Scoped(this, tenant);

        private sealed class Scoped(Rows owner, ITenantContext tenant) : IRepositoryObjectRepository
        {
            public Task<RepositoryObject> CreateAsync(RepositoryObject repositoryObject, CancellationToken ct = default)
            {
                Assert.Equal(tenant.TenantId, repositoryObject.TenantId);
                lock (owner._rows) { owner._rows.Add(repositoryObject); }
                return Task.FromResult(repositoryObject);
            }

            public Task<RepositoryObject?> GetByContentIdAsync(Guid contentId, CancellationToken ct = default)
            {
                var current = tenant.TenantId;
                lock (owner._rows) { return Task.FromResult(owner._rows.FirstOrDefault(r => r.ContentId == contentId && r.TenantId == current)); }
            }

            public Task<IReadOnlyList<RepositoryObject>> GetByOwningItemAsync(Guid owningItemId, CancellationToken ct = default) => throw new NotSupportedException();
            public Task<bool> MarkCompensatedAsync(Guid contentId, CancellationToken ct = default) => throw new NotSupportedException();
        }
    }

    private sealed class Tenants : ITenantRegistryRepository
    {
        public Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(id == TenantA || id == TenantB || id == Suspended
                ? new Tenant { Id = id, Code = "T", Slug = "t", Name = "t", DisplayName = "T", Domain = "t.test",
                    Status = id == Suspended ? TenantStatus.Suspended : TenantStatus.Active }
                : null);

        public Task<Tenant?> GetByCodeAsync(string code, CancellationToken ct = default) => Task.FromResult<Tenant?>(null);
        public Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default) => Task.FromResult<Tenant?>(null);
        public Task<Tenant?> GetByDomainAsync(string domain, CancellationToken ct = default) => Task.FromResult<Tenant?>(null);
        public Task<IReadOnlyList<Tenant>> GetActiveTenantsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Tenant>>([]);
        public Task<Tenant> CreateAsync(Tenant tenant, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateAsync(Tenant tenant, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateStatusAsync(Guid id, TenantStatus status, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Tenant>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Tenant>>([]);
        public Task<(IReadOnlyList<Tenant> Items, long TotalCount)> QueryAsync(TenantListQuery query, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<TenantRegistryStats> GetStatsAsync(CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class SystemActor : ICurrentUserContext
    {
        public Guid UserId => Guid.Empty;
        public string? Email => null;
        public string? DisplayName => null;
        public string ActorName => "system";
        public bool IsAuthenticated => false;
    }

    private sealed class Permissions(IEnumerable<string> keys) : IActorPermissionContext
    {
        private readonly HashSet<string> _keys = new(keys, StringComparer.Ordinal);
        public bool IsPlatformActor => false;
        public bool Has(string? permissionKey) => permissionKey is not null && _keys.Contains(permissionKey);
    }
}
