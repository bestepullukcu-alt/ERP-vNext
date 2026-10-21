using System.Security.Cryptography;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Contracts.DocumentRepository;
using Diten.Platform.Application.Features.DocumentRepository.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.DocumentRepository.Internal;

/// <summary>
/// WP-VW-W6 PLT-W6-DOC — what a service (CRM's system transmission) sends to <c>POST api/internal/document-repository/objects</c>.
/// There is no user behind it: the tenant comes from <paramref name="TenantId"/>, the asking module from
/// <paramref name="SourceModule"/>. The bytes travel as base64 — the one exception to AD-4's "no binary in a JSON body",
/// allowed because the only scope this door writes caps a file at 256 KB (<see cref="CommercialPreOrderDocumentFileRule"/>).
/// </summary>
public sealed record InternalDocumentStoreRequest(
    Guid TenantId,
    string? SourceModule,
    string? Scope,
    Guid OwnerItemId,
    string? FileName,
    string? ContentType,
    string? ContentBase64);

/// <summary>The stored document: its MOD-0262 content id (the <c>documentRef</c> of the internal dispatch), size and SHA-256
/// (lower-case hex). Never an object key.</summary>
public sealed record InternalDocumentStoredDto(Guid DocumentRef, long SizeBytes, string Sha256);

/// <summary>A document's bytes as the internal read answers them.</summary>
public sealed record InternalDocumentContent(Stream Content, string ContentType, string FileName, long SizeBytes);

/// <summary>
/// WP-VW-W6 PLT-W6-DOC — which module may write and read which scope through the key-only door. An explicit allow list
/// (CT decision 2026-10-21): CRM writes ONLY <see cref="ContentStorageScope.CommercialPreOrderDocument"/> and reads ONLY its
/// own two scopes (<see cref="ContentStorageScope.CrmVisitEvidence"/> — the pharmacist's signature W6-BE-a stored — and
/// <see cref="ContentStorageScope.CommercialPreOrderDocument"/>). Every other pair is refused; nothing here widens the user
/// path's keys.
/// </summary>
public static class InternalDocumentRepositoryAccess
{
    /// <summary>The asking module's owner: a service names itself by its manifest code or a Blueprint module id it hosts.</summary>
    public static readonly IReadOnlyDictionary<string, string> Owners =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["CRM"] = "CRM",
            // MOD-0168 Order Capture lives in Diten.CrmService (K-O1).
            ["MOD-0168"] = "CRM"
        };

    private static readonly IReadOnlyDictionary<string, ContentStorageScope[]> Writes =
        new Dictionary<string, ContentStorageScope[]>(StringComparer.Ordinal)
        {
            ["CRM"] = [ContentStorageScope.CommercialPreOrderDocument]
        };

    private static readonly IReadOnlyDictionary<string, ContentStorageScope[]> Reads =
        new Dictionary<string, ContentStorageScope[]>(StringComparer.Ordinal)
        {
            ["CRM"] = [ContentStorageScope.CrmVisitEvidence, ContentStorageScope.CommercialPreOrderDocument]
        };

    public static string? OwnerOf(string? sourceModule) =>
        !string.IsNullOrWhiteSpace(sourceModule) && Owners.TryGetValue(sourceModule.Trim(), out var owner) ? owner : null;

    public static bool CanWrite(string? sourceModule, ContentStorageScope scope) =>
        OwnerOf(sourceModule) is { } owner && Writes.TryGetValue(owner, out var scopes) && scopes.Contains(scope);

    /// <summary>The stored row's scope travels by NAME (<c>RepositoryObject.Scope</c>).</summary>
    public static bool CanRead(string? sourceModule, string? storedScope) =>
        OwnerOf(sourceModule) is { } owner
        && Reads.TryGetValue(owner, out var scopes)
        && Enum.TryParse<ContentStorageScope>(storedScope, ignoreCase: false, out var scope)
        && scopes.Contains(scope);
}

/// <summary>
/// WP-VW-W6 PLT-W6-DOC — the key-only store, audited like the user path's <c>StoreRepositoryObjectCommand</c>: the record
/// names the asking module and the body's tenant; the actor is the System (no principal — BL-409).
/// </summary>
public sealed record StoreInternalRepositoryObjectCommand(
    Guid TenantId,
    string SourceModule,
    StoreRepositoryObjectInput Input,
    string CorrelationId)
    : IRequest<Response<RepositoryObjectModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.DocumentManagement,
        AuditOperation.Create,
        "RepositoryObject",
        EntityId: Input.OwningItemId,
        SourceModule: SourceModule,
        TargetTenantId: TenantId,
        Metadata: new Dictionary<string, object?>
        {
            ["scope"] = Input.Scope.ToString(),
            ["channel"] = "internal-key"
        });
}

public sealed class StoreInternalRepositoryObjectHandler
    : IRequestHandler<StoreInternalRepositoryObjectCommand, Response<RepositoryObjectModel>>
{
    private readonly DocumentRepositoryService _service;

    public StoreInternalRepositoryObjectHandler(DocumentRepositoryService service) => _service = service;

    public Task<Response<RepositoryObjectModel>> Handle(StoreInternalRepositoryObjectCommand request, CancellationToken ct)
    {
        // AD-5 — the door checked the pair already; the boundary checks it again.
        if (!InternalDocumentRepositoryAccess.CanWrite(request.SourceModule, request.Input.Scope))
        {
            return Task.FromResult(Response<RepositoryObjectModel>.Fail(
                "This module may not write to this scope.", 403, DocumentRepositoryReasonCodes.Forbidden, request.CorrelationId));
        }

        return _service.StoreAsync(request.Input, request.CorrelationId, ct);
    }
}

public interface IInternalDocumentRepositoryService
{
    Task<Response<InternalDocumentStoredDto>> StoreAsync(InternalDocumentStoreRequest request, string correlationId, CancellationToken ct = default);

    Task<Response<InternalDocumentContent>> OpenReadAsync(Guid tenantId, string? sourceModule, Guid documentRef, string correlationId,
        CancellationToken ct = default);
}

/// <summary>
/// WP-VW-W6 PLT-W6-DOC — the internal document door. The key check is the controller's; everything after it is here, in
/// this order: the request's shape (400) → the tenant exists and is Active (404, naming nothing) → the (module, scope) pair
/// (write: 403 — the caller named the scope; read: 404 — like another tenant's object) → the tenant is pinned from the
/// body for the write, its audit record and the read. The repository's own tenant filter does the rest: another
/// tenant's documentRef is simply not found.
/// </summary>
public sealed class InternalDocumentRepositoryService : IInternalDocumentRepositoryService
{
    public const string ReasonBadRequest = "INTERNAL_DOCUMENT_BAD_REQUEST";
    public const int MaxFileNameLength = 200;
    private static readonly byte[] PdfMagic = "%PDF-"u8.ToArray();

    private readonly ITenantRegistryRepository _tenants;
    private readonly ITenantContext _tenantContext;
    private readonly IMediator _mediator;
    private readonly IRepositoryObjectRepository _objects;
    private readonly IContentStorageGateway _storage;
    private readonly ContentStorageOptions _options;
    private readonly ILogger<InternalDocumentRepositoryService> _logger;

    public InternalDocumentRepositoryService(
        ITenantRegistryRepository tenants,
        ITenantContext tenantContext,
        IMediator mediator,
        IRepositoryObjectRepository objects,
        IContentStorageGateway storage,
        Microsoft.Extensions.Options.IOptions<ContentStorageOptions> options,
        ILogger<InternalDocumentRepositoryService> logger)
    {
        _tenants = tenants;
        _tenantContext = tenantContext;
        _mediator = mediator;
        _objects = objects;
        _storage = storage;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>The size cap of the one scope this door writes (configurable; the default is 256 KB).</summary>
    private long MaxBytes
    {
        get
        {
            IContentStorageFileRule rule = (_options.Scopes ?? new ContentStorageScopeRules()).CommercialPreOrderDocument;
            return rule.MaxFileSizeBytes > 0 ? rule.MaxFileSizeBytes : rule.DefaultMaxFileSizeBytes;
        }
    }

    public async Task<Response<InternalDocumentStoredDto>> StoreAsync(
        InternalDocumentStoreRequest request, string correlationId, CancellationToken ct = default)
    {
        var (bytes, scope, shapeError) = Shape(request);
        if (shapeError is not null)
        {
            return Response<InternalDocumentStoredDto>.Fail(shapeError, 400, ReasonBadRequest, correlationId);
        }

        if (!await IsActiveTenantAsync(request.TenantId, ct))
        {
            return Response<InternalDocumentStoredDto>.Fail("Not found.", 404, DocumentRepositoryReasonCodes.NotFoundNonLeakage, correlationId);
        }

        var sourceModule = request.SourceModule!.Trim();
        if (!InternalDocumentRepositoryAccess.CanWrite(sourceModule, scope))
        {
            _logger.LogWarning(
                "document_repository.internal_write_refused SourceModule={SourceModule} Scope={Scope}", sourceModule, scope);
            return Response<InternalDocumentStoredDto>.Fail(
                "This module may not write to this scope.", 403, DocumentRepositoryReasonCodes.Forbidden, correlationId);
        }

        using var tenantScope = TenantScope.Begin(_tenantContext, request.TenantId);
        await using var content = new MemoryStream(bytes!, writable: false);
        var stored = await _mediator.Send(
            new StoreInternalRepositoryObjectCommand(
                request.TenantId,
                sourceModule,
                // One version per stored file: a retry that renders the document again never overwrites the earlier one.
                new StoreRepositoryObjectInput(scope, Guid.Empty, request.OwnerItemId, Guid.NewGuid(), request.FileName!.Trim(),
                    request.ContentType!.Trim().ToLowerInvariant(), content),
                correlationId),
            ct);

        if (!stored.IsSuccessful || stored.Data is null)
        {
            return Response<InternalDocumentStoredDto>.Fail(stored.Errors, stored.StatusCode, stored.ReasonCode, correlationId);
        }

        return Response<InternalDocumentStoredDto>.Success(
            new InternalDocumentStoredDto(stored.Data.ContentId, stored.Data.ByteSize, Sha256Hex(bytes!)), 201, correlationId);
    }

    public async Task<Response<InternalDocumentContent>> OpenReadAsync(
        Guid tenantId, string? sourceModule, Guid documentRef, string correlationId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty || documentRef == Guid.Empty || string.IsNullOrWhiteSpace(sourceModule))
        {
            return Response<InternalDocumentContent>.Fail("tenantId, sourceModule and documentRef are required.", 400, ReasonBadRequest, correlationId);
        }

        if (InternalDocumentRepositoryAccess.OwnerOf(sourceModule) is null || !await IsActiveTenantAsync(tenantId, ct))
        {
            return NotFound(correlationId);
        }

        using var tenantScope = TenantScope.Begin(_tenantContext, tenantId);
        var row = await _objects.GetByContentIdAsync(documentRef, ct);
        // Another tenant's object, a compensated one and another scope's all read as "not found" (non-leakage).
        if (row is null || row.TenantId != tenantId || !InternalDocumentRepositoryAccess.CanRead(sourceModule, row.Scope))
        {
            return NotFound(correlationId);
        }

        var stream = await _storage.OpenReadAsync(row.StorageProvider, row.ObjectKey, ct);
        if (!stream.IsSuccessful || stream.Data is null)
        {
            return Response<InternalDocumentContent>.Fail(
                stream.Errors, stream.StatusCode == 0 ? 503 : stream.StatusCode,
                stream.ReasonCode ?? DocumentRepositoryReasonCodes.StorageUnavailable, correlationId);
        }

        return Response<InternalDocumentContent>.Success(
            new InternalDocumentContent(stream.Data.Content, row.MediaType, row.FileName, row.ByteSize), correlationId: correlationId);
    }

    private (byte[]? Bytes, ContentStorageScope Scope, string? Error) Shape(InternalDocumentStoreRequest? request)
    {
        if (request is null)
        {
            return (null, default, "A body is required.");
        }

        if (request.TenantId == Guid.Empty)
        {
            return (null, default, "tenantId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.SourceModule))
        {
            return (null, default, "sourceModule is required.");
        }

        // The scope by NAME only: a number would let a caller reach a scope by ordinal.
        if (string.IsNullOrWhiteSpace(request.Scope)
            || request.Scope.Trim().All(char.IsDigit)
            || !Enum.TryParse<ContentStorageScope>(request.Scope.Trim(), ignoreCase: false, out var scope)
            || !Enum.IsDefined(scope))
        {
            return (null, default, "scope is required and must be a storage scope name.");
        }

        if (request.OwnerItemId == Guid.Empty)
        {
            return (null, scope, "ownerItemId is required.");
        }

        var fileName = request.FileName?.Trim();
        if (string.IsNullOrEmpty(fileName) || fileName.Length > MaxFileNameLength
            || fileName.IndexOfAny(['/', '\\', ':']) >= 0 || fileName.Contains("..", StringComparison.Ordinal) || fileName.Any(char.IsControl))
        {
            return (null, scope, "fileName is a plain file name.");
        }

        if (!string.Equals(request.ContentType?.Trim(), "application/pdf", StringComparison.OrdinalIgnoreCase)
            || !fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return (null, scope, "Only a PDF (application/pdf, .pdf) is accepted.");
        }

        var base64 = request.ContentBase64?.Trim();
        // Refused before decoding: a base64 text longer than the cap's encoding cannot fit.
        if (string.IsNullOrEmpty(base64) || base64.Length > ((MaxBytes + 2) / 3 * 4) + 4)
        {
            return (null, scope, string.IsNullOrEmpty(base64) ? "contentBase64 is required." : $"The file may be at most {MaxBytes} bytes.");
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(base64);
        }
        catch (FormatException)
        {
            return (null, scope, "contentBase64 is not base64.");
        }

        if (bytes.Length == 0)
        {
            return (null, scope, "The file is empty.");
        }

        if (bytes.Length > MaxBytes)
        {
            return (null, scope, $"The file may be at most {MaxBytes} bytes.");
        }

        if (!bytes.AsSpan().StartsWith(PdfMagic))
        {
            return (null, scope, "The content is not a PDF.");
        }

        return (bytes, scope, null);
    }

    private async Task<bool> IsActiveTenantAsync(Guid tenantId, CancellationToken ct)
    {
        var tenant = await _tenants.GetByIdAsync(tenantId, ct);
        return tenant is not null && tenant.Status == TenantStatus.Active;
    }

    private static string Sha256Hex(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static Response<InternalDocumentContent> NotFound(string correlationId) =>
        Response<InternalDocumentContent>.Fail("Not found.", 404, DocumentRepositoryReasonCodes.NotFoundNonLeakage, correlationId);
}
