using System.Security.Cryptography;
using System.Text;
using Diten.Platform.Application.Contracts.DocumentRepository;
using Diten.Platform.Infrastructure.Services.DocumentManagement;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Platform.Application.Tests.DocumentRepository;

/// <summary>
/// MOD-0262-FU01 — storage gateway behaviour: the validation matrix, object-key determinism and tenant
/// separation, and checksum ownership. These exercise the REAL local-filesystem provider against a temp root,
/// because the defects that matter here (a traversal-shaped key, a checksum taken on trust, a size limit that
/// only fires after the bytes already landed) are invisible to a fake.
/// </summary>
public sealed class DocumentBinaryStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "DitenStorageTests", Guid.NewGuid().ToString("N"));

    private LocalFileSystemContentStorageGateway CreateGateway(long maxBytes = 1_048_576) =>
        new(Options.Create(new ContentStorageOptions
        {
            RootPath = _root,
            MaxFileSizeBytes = maxBytes,
            AllowedExtensions = [".txt", ".pdf"],
            AllowedMediaTypes = []
        }), NullLogger<LocalFileSystemContentStorageGateway>.Instance);

    private static ContentStoreRequest Request(
        Guid tenantId, Stream content, string fileName = "note.txt", string? mediaType = "text/plain",
        Guid? companyId = null, Guid? itemId = null, Guid? versionId = null) =>
        new(tenantId, companyId ?? Guid.Empty, ContentStorageScope.Documents,
            itemId ?? Guid.Empty, versionId ?? Guid.Empty, fileName, mediaType, content, "tester");

    private static MemoryStream Bytes(string s) => new(Encoding.UTF8.GetBytes(s), writable: false);

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); } catch { }
    }

    // ── validation matrix ────────────────────────────────────────────────────

    [Fact]
    public async Task Disallowed_extension_is_rejected()
    {
        var result = await CreateGateway().StoreAsync(Request(Guid.NewGuid(), Bytes("x"), "payload.exe"));

        Assert.False(result.IsSuccessful);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(DocumentRepositoryReasonCodes.ValidationFailed, result.ReasonCode);
    }

    [Fact]
    public async Task Empty_content_is_rejected_and_leaves_nothing_behind()
    {
        var result = await CreateGateway().StoreAsync(Request(Guid.NewGuid(), new MemoryStream()));

        Assert.False(result.IsSuccessful);
        Assert.Equal(400, result.StatusCode);
        Assert.Empty(Directory.Exists(_root)
            ? Directory.GetFiles(_root, "*", SearchOption.AllDirectories)
            : []);
    }

    // The size limit must fire DURING the copy, and the partial object must be removed. A limit that only
    // rejects after the whole payload is on disk still lets an over-size upload consume the disk.
    [Fact]
    public async Task Oversize_content_is_rejected_and_the_partial_object_is_removed()
    {
        var gateway = CreateGateway(maxBytes: 16);

        var result = await gateway.StoreAsync(Request(Guid.NewGuid(), Bytes(new string('a', 512))));

        Assert.False(result.IsSuccessful);
        Assert.Equal(400, result.StatusCode);
        Assert.Empty(Directory.Exists(_root)
            ? Directory.GetFiles(_root, "*", SearchOption.AllDirectories)
            : []);
    }

    // ── checksum ownership ───────────────────────────────────────────────────

    [Fact]
    public async Task Checksum_is_computed_by_the_store_over_the_streamed_bytes()
    {
        const string payload = "the quick brown fox";
        var expected = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();

        var result = await CreateGateway().StoreAsync(Request(Guid.NewGuid(), Bytes(payload)));

        Assert.True(result.IsSuccessful);
        Assert.Equal(expected, result.Data!.Checksum);
        Assert.Equal(payload.Length, result.Data.ByteSize);
    }

    // ── object-key determinism and tenant separation ─────────────────────────

    [Fact]
    public async Task Same_inputs_produce_the_same_object_key()
    {
        var tenantId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var gateway = CreateGateway();

        var first = await gateway.StoreAsync(Request(tenantId, Bytes("one"), itemId: itemId, versionId: versionId));
        var second = await gateway.StoreAsync(Request(tenantId, Bytes("two"), itemId: itemId, versionId: versionId));

        Assert.True(first.IsSuccessful);
        Assert.True(second.IsSuccessful);
        Assert.Equal(first.Data!.ObjectKey, second.Data!.ObjectKey);
    }

    // Tenant isolation is structural: it lives in the key, not only in a query filter. Two tenants uploading
    // an identically named file must never collide on the same physical object.
    [Fact]
    public async Task Two_tenants_uploading_the_same_file_name_get_separate_object_keys()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var gateway = CreateGateway();

        var a = await gateway.StoreAsync(Request(tenantA, Bytes("a"), "same-name.txt"));
        var b = await gateway.StoreAsync(Request(tenantB, Bytes("b"), "same-name.txt"));

        Assert.True(a.IsSuccessful);
        Assert.True(b.IsSuccessful);
        Assert.NotEqual(a.Data!.ObjectKey, b.Data!.ObjectKey);
        Assert.Contains(tenantA.ToString("D"), a.Data.ObjectKey, StringComparison.Ordinal);
        Assert.Contains(tenantB.ToString("D"), b.Data.ObjectKey, StringComparison.Ordinal);
    }

    [Fact]
    public async Task File_names_are_sanitized_so_a_traversal_name_cannot_escape_the_root()
    {
        var result = await CreateGateway().StoreAsync(Request(Guid.NewGuid(), Bytes("x"), "../../escape.txt"));

        Assert.True(result.IsSuccessful);
        Assert.DoesNotContain("..", result.Data!.ObjectKey, StringComparison.Ordinal);
    }

    // ── read path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Stored_content_can_be_streamed_back_byte_for_byte()
    {
        const string payload = "round trip";
        var gateway = CreateGateway();
        var stored = await gateway.StoreAsync(Request(Guid.NewGuid(), Bytes(payload)));

        var opened = await gateway.OpenReadAsync(stored.Data!.StorageProvider, stored.Data.ObjectKey);

        Assert.True(opened.IsSuccessful);
        using var reader = new StreamReader(opened.Data!.Content);
        Assert.Equal(payload, await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task An_unknown_object_key_reads_as_not_found_without_leaking_existence()
    {
        var opened = await CreateGateway().OpenReadAsync(LocalFileSystemContentStorageGateway.ProviderName, "tenant-x/documents/missing.txt");

        Assert.False(opened.IsSuccessful);
        Assert.Equal(404, opened.StatusCode);
        Assert.Equal(DocumentRepositoryReasonCodes.NotFoundNonLeakage, opened.ReasonCode);
    }

    // A raw key is never an accepted input at the API, but the provider still refuses traversal defensively.
    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("/etc/passwd")]
    [InlineData("tenant-1/../../secret.txt")]
    public async Task Traversal_shaped_object_keys_are_refused(string objectKey)
    {
        var opened = await CreateGateway().OpenReadAsync(LocalFileSystemContentStorageGateway.ProviderName, objectKey);

        Assert.False(opened.IsSuccessful);
        Assert.Equal(404, opened.StatusCode);
    }

    // ── compensation, NOT purge ──────────────────────────────────────────────

    [Fact]
    public async Task TryDelete_removes_the_stored_object_and_reports_success()
    {
        var gateway = CreateGateway();
        var stored = await gateway.StoreAsync(Request(Guid.NewGuid(), Bytes("gone")));

        Assert.True(await gateway.TryDeleteAsync(stored.Data!.StorageProvider, stored.Data.ObjectKey));

        var opened = await gateway.OpenReadAsync(stored.Data.StorageProvider, stored.Data.ObjectKey);
        Assert.Equal(404, opened.StatusCode);
    }
}
