using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Observability;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.DocumentRepository;
using Diten.Platform.Application.Features.DocumentRepository;
using Diten.Platform.Application.Features.DocumentRepository.Commands;
using Diten.Platform.Application.Features.DocumentRepository.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers;

/// <summary>
/// MOD-0262-FU01 — Internal Document Repository Service API (Document Binary Store).
/// <para>
/// ⛔ <b>AD-5.</b> Authorisation happens here and again inside <c>DocumentRepositoryService</c>. The seam's old
/// "the caller already authorised" contract is revoked: this controller never trusts an upstream claim, and the
/// gateway performs no authentication of its own (there are zero <c>AuthenticationOptions</c> across the Ocelot
/// routes), so the service boundary is the only enforcement point.
/// </para>
/// <para>
/// ⛔ <b>AD-4.</b> Upload is <c>multipart/form-data</c> and download is a stream. No endpoint accepts or returns
/// a binary payload inside a JSON body, and the internal <c>ObjectKey</c> is never serialised to a client.
/// </para>
/// <para>
/// ⛔ <b>AD-6.</b> There is no purge/destroy/scheduler endpoint here. The single removal endpoint is
/// <b>compensation</b> for a failed consumer commit; physical destruction under a retention decision is
/// MOD-0262-FU05 and requires <c>platform.document-repository.purge.execute</c>, which FU01 does not define.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/document-repository")]
[Authorize]
public sealed class DocumentRepositoryController : CustomBaseController
{
    private readonly IMediator _mediator;
    private readonly ICorrelationContext _correlationContext;

    public DocumentRepositoryController(IMediator mediator, ICorrelationContext correlationContext)
    {
        _mediator = mediator;
        _correlationContext = correlationContext;
    }

    private string CorrelationId => _correlationContext.CorrelationId;

    /// <summary>
    /// Stores one binary. The payload streams straight from the request to the provider — it is never buffered
    /// whole, and the size limit is enforced during the copy rather than by pre-reading the body.
    /// </summary>
    [HttpPost("objects")]
    [HasPermission(DocumentRepositoryPermissions.Manage)]
    [RequestSizeLimit(long.MaxValue)]
    [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
    public async Task<IActionResult> Store(
        [FromForm] IFormFile? file,
        [FromForm] Guid owningItemId,
        [FromForm] Guid owningVersionId,
        [FromForm] Guid companyId,
        [FromForm] ContentStorageScope scope,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return CreateActionResultInstance(Response<RepositoryObjectModel>.Fail(
                "A file is required.", 400, DocumentRepositoryReasonCodes.ValidationFailed, CorrelationId));
        }

        await using var content = file.OpenReadStream();

        var input = new StoreRepositoryObjectInput(
            scope, companyId, owningItemId, owningVersionId, file.FileName, file.ContentType, content);

        return CreateActionResultInstance(
            await _mediator.Send(new StoreRepositoryObjectCommand(input, CorrelationId), ct));
    }

    /// <summary>Pointer metadata. The response model has no <c>ObjectKey</c> member at all.</summary>
    [HttpGet("objects/{contentId:guid}")]
    [HasPermission(DocumentRepositoryPermissions.Read)]
    public async Task<IActionResult> Get(Guid contentId, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetRepositoryObjectByIdQuery(contentId, CorrelationId), ct));

    /// <summary>
    /// Streams the stored bytes. Addressed by content id only: an object key is never an accepted input, so
    /// possessing a raw key grants nothing, and another tenant's content id resolves to 404 (non-leakage).
    /// </summary>
    [HttpGet("objects/{contentId:guid}/content")]
    [HasPermission(DocumentRepositoryPermissions.Read)]
    public async Task<IActionResult> Content(Guid contentId, CancellationToken ct)
    {
        var response = await _mediator.Send(new OpenRepositoryObjectContentQuery(contentId, CorrelationId), ct);
        if (!response.IsSuccessful || response.Data is null)
        {
            return CreateActionResultInstance(response);
        }

        return File(response.Data.Content, response.Data.MediaType, response.Data.FileName);
    }

    /// <summary>
    /// ⛔ Compensation for a failed consumer-side metadata commit — <b>not</b> a deletion feature and not a
    /// purge path (AD-6). It evaluates no retention policy and checks no legal hold, because it is not the
    /// destruction path.
    /// </summary>
    [HttpPost("objects/{contentId:guid}/compensate")]
    [HasPermission(DocumentRepositoryPermissions.Manage)]
    public async Task<IActionResult> Compensate(Guid contentId, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CompensateRepositoryObjectCommand(contentId, CorrelationId), ct));
}
