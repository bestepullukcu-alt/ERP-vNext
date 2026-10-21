using System.Security.Cryptography;
using System.Text;
using Diten.Platform.API.Controllers.Common;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentRepository.Internal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Internal;

/// <summary>
/// WP-VW-W6 PLT-W6-DOC — the server-to-server door into the MOD-0262 document repository for a service's system step (CRM's
/// pre-order transmission writes the depot document and reads the pharmacist's signature, with no user behind the call).
/// Lives under <c>/api/internal/*</c> (not routed by the Gateway, bypassed by tenant resolution) and is gated by the shared
/// internal API key, checked BEFORE anything is read: without the key the answer is 401 whether or not the tenant or the
/// document exists. A call that carries a user token is refused too (401): this door is for services, and a user goes
/// through <c>api/v1/document-repository</c> and its keys. The tenant is pinned from the request inside
/// <see cref="IInternalDocumentRepositoryService"/>, which also decides the (module, scope) pair.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/internal/document-repository")]
public sealed class InternalDocumentRepositoryController : CustomBaseController
{
    private const string InternalApiKeyHeader = "X-Internal-Api-Key";
    private const string CorrelationIdHeader = "X-Correlation-Id";

    private readonly IInternalDocumentRepositoryService _documents;
    private readonly IConfiguration _configuration;
    private readonly ILogger<InternalDocumentRepositoryController> _logger;

    public InternalDocumentRepositoryController(
        IInternalDocumentRepositoryService documents,
        IConfiguration configuration,
        ILogger<InternalDocumentRepositoryController> logger)
    {
        _documents = documents;
        _configuration = configuration;
        _logger = logger;
    }

    private string CorrelationId => Request.Headers[CorrelationIdHeader].FirstOrDefault() ?? HttpContext.TraceIdentifier;

    /// <summary>Stores one PDF; the answer is <c>{ documentRef, sizeBytes, sha256 }</c> (201).</summary>
    [HttpPost("objects")]
    [RequestSizeLimit(1_048_576)]
    public async Task<IActionResult> Store(
        [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] InternalDocumentStoreRequest? request,
        CancellationToken ct)
    {
        if (Refusal() is { } refused)
        {
            return refused;
        }

        if (request is null)
        {
            return CreateActionResultInstance(Response<InternalDocumentStoredDto>.Fail(
                "A body is required.", 400, InternalDocumentRepositoryService.ReasonBadRequest, CorrelationId));
        }

        return CreateActionResultInstance(await _documents.StoreAsync(request, CorrelationId, ct));
    }

    /// <summary>The stored bytes of <paramref name="documentRef"/> in the given tenant, if the asking module may read its
    /// scope; anything else is 404.</summary>
    [HttpGet("objects/{documentRef:guid}")]
    public async Task<IActionResult> Read(Guid documentRef, [FromQuery] Guid tenantId, [FromQuery] string? sourceModule, CancellationToken ct)
    {
        if (Refusal() is { } refused)
        {
            return refused;
        }

        var response = await _documents.OpenReadAsync(tenantId, sourceModule, documentRef, CorrelationId, ct);
        if (!response.IsSuccessful || response.Data is null)
        {
            return CreateActionResultInstance(response);
        }

        return File(response.Data.Content, response.Data.ContentType, response.Data.FileName);
    }

    /// <summary>401 without the right internal key, and 401 for a call that carries a user token.</summary>
    private IActionResult? Refusal()
    {
        if (!IsInternalRequestAuthorized())
        {
            _logger.LogWarning("Internal document repository call rejected. CorrelationId={CorrelationId}", CorrelationId);
            return CreateActionResultInstance(Response<InternalDocumentStoredDto>.Fail("Unauthorized.", 401));
        }

        if (Request.Headers.ContainsKey("Authorization"))
        {
            _logger.LogWarning("Internal document repository call carried a user token. CorrelationId={CorrelationId}", CorrelationId);
            return CreateActionResultInstance(Response<InternalDocumentStoredDto>.Fail("A user token is not accepted here.", 401));
        }

        return null;
    }

    private bool IsInternalRequestAuthorized()
    {
        // The same shared internal secret Platform's other internal endpoints use (InternalNotificationEventDispatchController).
        var expected = _configuration["AuthService:InternalApiKey"];
        if (string.IsNullOrWhiteSpace(expected) || !Request.Headers.TryGetValue(InternalApiKeyHeader, out var providedValues))
        {
            return false;
        }

        var provided = providedValues.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(provided))
        {
            return false;
        }

        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        return expectedBytes.Length == providedBytes.Length
               && CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }
}
