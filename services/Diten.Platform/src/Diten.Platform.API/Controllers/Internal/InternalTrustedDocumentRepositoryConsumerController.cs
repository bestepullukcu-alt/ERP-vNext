using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Internal;

/// <summary>
/// MOD-0262-FU01 DL-2 — the document repository for a TRUSTED CONSUMER SERVICE (v1: MOD-0217 CLM through
/// <c>Diten.MDM</c>), called with its own Auth-signed service token, never with a user token (KARAR D56/2).
/// <para>
/// <b>Gate (r4, F-1).</b> <c>[AllowAnonymous]</c> to the framework; the whole gate is
/// <see cref="ITrustedDocumentRepositoryConsumerRequestExecutor"/>: header pre-check, explicit authentication with the
/// <c>TrustedDocumentRepositoryConsumerService</c> scheme, closed consumer list, the token's tenant, scope, size, and the
/// refusal records. A class-level scheme <c>[Authorize]</c> would refuse before the executor with a bodiless 401 and no
/// record (measured, §8.1), so there is none — and no <c>[HasPermission]</c>: a service passes no user permission (§14).
/// </para>
/// <para>
/// Under <c>/api/internal</c>, which the Gateway does not route (§15). ⛔ AD-4: the upload is read as a stream with
/// <c>MultipartReader</c> — no form-file parameter, no form binding, no framework temp file. ⛔ AD-6: compensation only;
/// there is no purge, destroy or delete action.
/// </para>
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/internal/v1/document-repository/trusted-consumer")]
public sealed class InternalTrustedDocumentRepositoryConsumerController : CustomBaseController
{
    private readonly ITrustedDocumentRepositoryConsumerRequestExecutor _executor;

    public InternalTrustedDocumentRepositoryConsumerController(ITrustedDocumentRepositoryConsumerRequestExecutor executor)
    {
        _executor = executor;
    }

    /// <summary>Streams one file (≤ 50 MiB) with its four form fields; 201 + <c>RepositoryObjectModel</c>.</summary>
    [HttpPost("objects")]
    [RequestSizeLimit(TrustedDocumentRepositoryUploadLimits.MaxRequestBytes)]
    public Task<IActionResult> Store() => _executor.StoreAsync(HttpContext);

    /// <summary>Pointer metadata of the consumer's own object; never <c>ObjectKey</c>.</summary>
    [HttpGet("objects/{contentId:guid}")]
    public Task<IActionResult> Get(Guid contentId) => _executor.GetAsync(HttpContext, contentId);

    /// <summary>Streams the consumer's own object; the read is audited before the first byte.</summary>
    [HttpGet("objects/{contentId:guid}/content")]
    public Task<IActionResult> Content(Guid contentId) => _executor.OpenContentAsync(HttpContext, contentId);

    /// <summary>⛔ Compensation of a failed consumer commit — repeat-safe (204), not a purge (AD-6).</summary>
    [HttpPost("objects/{contentId:guid}/compensate")]
    public Task<IActionResult> Compensate(Guid contentId) => _executor.CompensateAsync(HttpContext, contentId);
}
