using System.Security.Cryptography;
using System.Text;
using Diten.Platform.Application.Contracts.DocumentRepository;
using Diten.Platform.Infrastructure.Services.DocumentManagement;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Platform.Application.Tests.DocumentRepository;

/// <summary>
/// WP-SCMM-16A (CAND-CAP-0011 / SCMM-16) — the ONLY additive gap over the existing MOD-0262-FU01 document
/// repository surface was a new <see cref="ContentStorageScope.ContentMessagingArtifacts"/> value. These tests
/// pin the two things that make that value safe: (1) it lands in its OWN on-disk partition rather than being
/// silently aliased into <c>documents</c> by the <c>_ =&gt; "documents"</c> default (the exact MOD-0024 trap the
/// gateway comment warns about), and (2) the render use case (store a PDF, read the identical bytes back, with a
/// SHA-256 checksum) works end to end through the unchanged gateway. Existing scope→segment mappings are pinned
/// alongside so a future edit cannot collapse the switch without a red test.
/// </summary>
public sealed class ContentMessagingArtifactsScopeTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "DitenScmm16aTests", Guid.NewGuid().ToString("N"));

    private LocalFileSystemContentStorageGateway BuildGateway() => new(
        Options.Create(new ContentStorageOptions
        {
            RootPath = _root,
            MaxFileSizeBytes = 1_048_576,
            AllowedExtensions = [".pdf"],
            AllowedMediaTypes = []
        }),
        NullLogger<LocalFileSystemContentStorageGateway>.Instance);

    private static ContentStoreRequest Request(
        ContentStorageScope scope, Guid tenantId, Guid companyId, Guid itemId, Guid versionId, byte[] bytes) =>
        new(tenantId, companyId, scope, itemId, versionId, "artifact.pdf", "application/pdf",
            new MemoryStream(bytes), "render-service");

    [Fact]
    public async Task ContentMessagingArtifacts_lands_in_its_own_partition_never_the_documents_partition()
    {
        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        var stored = await BuildGateway().StoreAsync(
            Request(ContentStorageScope.ContentMessagingArtifacts, tenantId, companyId, itemId, versionId,
                Encoding.UTF8.GetBytes("%PDF-1.7 rendered content set revision")));

        Assert.True(stored.IsSuccessful);
        Assert.NotNull(stored.Data);

        var objectKey = stored.Data!.ObjectKey;
        // Its own segment — NOT aliased into the documents partition (the MOD-0024 trap).
        Assert.Contains("/content-messaging-artifacts/", objectKey);
        Assert.DoesNotContain("/documents/", objectKey);
        // The tenant prefix is structural (this is what the FU01 service's cross-tenant 404 guard leans on).
        Assert.Equal(
            $"tenant-{tenantId:D}/company-{companyId:D}/content-messaging-artifacts/{itemId:D}/versions/{versionId:D}/artifact.pdf",
            objectKey);
    }

    [Theory]
    [InlineData(ContentStorageScope.Documents, "documents")]
    [InlineData(ContentStorageScope.Templates, "templates")]
    [InlineData(ContentStorageScope.TaskAttachments, "task-attachments")]
    [InlineData(ContentStorageScope.ContentMessagingArtifacts, "content-messaging-artifacts")]
    public async Task Each_scope_maps_to_its_own_distinct_partition_segment(ContentStorageScope scope, string segment)
    {
        var stored = await BuildGateway().StoreAsync(
            Request(scope, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                Encoding.UTF8.GetBytes("payload")));

        Assert.True(stored.IsSuccessful);
        Assert.Contains($"/{segment}/", stored.Data!.ObjectKey);
    }

    [Fact]
    public async Task Store_computes_a_sha256_checksum_and_reads_the_identical_bytes_back()
    {
        var gateway = BuildGateway();
        var bytes = Encoding.UTF8.GetBytes("%PDF-1.7 deterministic artifact bytes for round trip");
        var expected = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        var stored = await gateway.StoreAsync(
            Request(ContentStorageScope.ContentMessagingArtifacts,
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), bytes));

        Assert.True(stored.IsSuccessful);
        Assert.Equal(expected, stored.Data!.Checksum);
        Assert.Equal(bytes.Length, stored.Data.ByteSize);

        var read = await gateway.OpenReadAsync(stored.Data.StorageProvider, stored.Data.ObjectKey);
        Assert.True(read.IsSuccessful);

        await using var buffer = new MemoryStream();
        await using (var content = read.Data!.Content)
        {
            await content.CopyToAsync(buffer);
        }
        Assert.Equal(bytes, buffer.ToArray());
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup of the per-test temp root; a leftover temp folder never fails the test.
        }
    }
}
