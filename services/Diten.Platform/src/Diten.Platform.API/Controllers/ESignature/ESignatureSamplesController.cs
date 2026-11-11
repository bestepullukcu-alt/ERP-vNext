using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Models.ESignature;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.ESignature;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.ESignature;

/// <summary>
/// MOD-0022 E3 — signature samples ("İmzam" + "İmza Örneği Onayları"), ESIGN-BUNDLE §4.17–4.19. Every call is the user's
/// own tenant token; the handlers refuse anyone who is not a tenant_user, and the owner can never decide their own sample.
/// </summary>
[Route("api/v1/esign/samples")]
public sealed class ESignatureSamplesController : CustomBaseController
{
    private readonly IMediator _mediator;

    public ESignatureSamplesController(IMediator mediator) => _mediator = mediator;

    /// <summary>§4.17 createSample — 201 Sample (pending or active by the tenant policy).</summary>
    [HttpPost]
    [HasPermission(ESignPermissions.MySamplesManage)]
    [RequestSizeLimit(1_600_000)]
    public async Task<IActionResult> Create([FromBody] CreateSampleRequest request, CancellationToken ct)
    {
        var strokes = request.Strokes is { ValueKind: not System.Text.Json.JsonValueKind.Null and not System.Text.Json.JsonValueKind.Undefined } s
            ? s.GetRawText()
            : null;
        return CreateActionResultInstance(await _mediator.Send(
            new CreateSampleCommand(request.Ticket, request.Kind, request.PngBase64, strokes, request.IdempotencyKey), ct));
    }

    /// <summary>§4.17 related — my samples, newest first.</summary>
    [HttpGet("mine")]
    [HasPermission(ESignPermissions.MySamplesManage)]
    public async Task<IActionResult> Mine(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new ListMySamplesQuery(), ct));

    /// <summary>
    /// §4.17 related — image/png for the owner, an approver, or anyone allowed to read a signature that shows it; else 404.
    /// Any one of the three keys opens the endpoint ([HasAnyPermission], WP-MVP1-SEC-ESIGN1 — visible to A1 and the guards);
    /// which of them the caller holds is still passed to the handler, which decides WHICH samples that caller may see.
    /// </summary>
    [HttpGet("{id:guid}/image")]
    [HasAnyPermission(ESignPermissions.MySamplesManage, ESignPermissions.SamplesApprove, ESignPermissions.SignaturesRead)]
    public async Task<IActionResult> Image(Guid id, CancellationToken ct)
    {
        bool Has(string key) => PermissionClaimEvaluator.Evaluate(User.Claims, key).IsSatisfied;
        var canApprove = Has(ESignPermissions.SamplesApprove);
        var canRead = Has(ESignPermissions.SignaturesRead);

        var result = await _mediator.Send(new GetSampleImageQuery(id, canApprove, canRead), ct);
        if (!result.IsSuccessful || result.Data is null)
        {
            return CreateActionResultInstance(result);
        }

        Response.Headers["X-Image-Hash"] = result.Data.ImageHash;
        return File(result.Data.Content, "image/png");
    }

    /// <summary>§4.18 deactivateSample — owner only.</summary>
    [HttpPost("{id:guid}/deactivate")]
    [HasPermission(ESignPermissions.MySamplesManage)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new DeactivateSampleCommand(id), ct));

    /// <summary>§4.18 withdrawSample — owner only; never a used or active sample.</summary>
    [HttpPost("{id:guid}/withdraw")]
    [HasPermission(ESignPermissions.MySamplesManage)]
    public async Task<IActionResult> Withdraw(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new WithdrawSampleCommand(id), ct));

    /// <summary>§4.19 listPendingSamples.</summary>
    [HttpGet("pending")]
    [HasPermission(ESignPermissions.SamplesApprove)]
    public async Task<IActionResult> Pending(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new ListPendingSamplesQuery(), ct));

    /// <summary>§4.19 approveSample — never by the owner (403 ESIGN_SELF_APPROVAL_FORBIDDEN).</summary>
    [HttpPost("{id:guid}/approve")]
    [HasPermission(ESignPermissions.SamplesApprove)]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new DecideSampleCommand(id, true, null), ct));

    /// <summary>§4.19 rejectSample — reason ≥ 10; never by the owner.</summary>
    [HttpPost("{id:guid}/reject")]
    [HasPermission(ESignPermissions.SamplesReject)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] ESignReasonRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new DecideSampleCommand(id, false, request.Reason), ct));

    /// <summary>§4.19 decision log.</summary>
    [HttpGet("decisions")]
    [HasPermission(ESignPermissions.SamplesApprove)]
    public async Task<IActionResult> Decisions(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new ListSampleDecisionsQuery(), ct));
}
