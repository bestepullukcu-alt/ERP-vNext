using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Models.ESignature;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.ESignature;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.ESignature;

// MOD-0022 E2 — the e-signature API, api/v1/esign/** (ESIGN-BUNDLE §4.4–4.16, 4.20–4.26). Tenant scope only: every route
// is outside /Platform, every key esign.* Scope = Tenant. The Gateway route /api/v1/esign/{everything} is INT-ESIGN's.
//
// [HasPermission] lets platform / partner administrators through by bypass; the handlers then refuse anyone who is not a
// tenant_user with their own token (ESIGN-BUNDLE rule 1) — a platform admin can never sign, verify or export.

[Route("api/v1/esign/signatures")]
public sealed class ESignatureSignaturesController : CustomBaseController
{
    private readonly IMediator _mediator;

    public ESignatureSignaturesController(IMediator mediator) => _mediator = mediator;

    /// <summary>§4.4 signSubject — 201 SignatureRecord (200 on an idempotent replay).</summary>
    [HttpPost]
    [HasPermission(ESignPermissions.SignaturesSign)]
    [RequestSizeLimit(1_600_000)]
    public async Task<IActionResult> Sign([FromBody] SignSubjectRequest request, CancellationToken ct)
    {
        var appearance = request.Appearance is null
            ? null
            : new SignAppearanceInput(request.Appearance.Type, request.Appearance.SampleId, request.Appearance.Drawing?.PngBase64,
                request.Appearance.Drawing?.Strokes is { ValueKind: not System.Text.Json.JsonValueKind.Null and not System.Text.Json.JsonValueKind.Undefined } strokes
                    ? strokes.GetRawText()
                    : null);
        var command = new SignSubjectCommand(request.Ticket, request.Subject?.Module, request.Subject?.SubjectType, request.Subject?.SubjectId,
            request.ActionCode, request.MeaningCode, request.Note, appearance, request.RequestId, request.Context, request.IdempotencyKey);
        return CreateActionResultInstance(await _mediator.Send(command, ct));
    }

    /// <summary>§4.24 searchSignatures — the export screen's table.</summary>
    [HttpGet]
    [HasPermission(ESignPermissions.SignaturesExport)]
    public async Task<IActionResult> Search([FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc, [FromQuery] string? module,
        [FromQuery] string? signerUserId, [FromQuery] string? meaningCode, [FromQuery] Guid? legalEntityId, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25, CancellationToken ct = default) =>
        CreateActionResultInstance(await _mediator.Send(
            new SearchSignaturesQuery(fromUtc, toUtc, module, signerUserId, meaningCode, legalEntityId, page, pageSize), ct));

    /// <summary>§4.6 listSignaturesBySubject.</summary>
    [HttpGet("by-subject")]
    [HasPermission(ESignPermissions.SignaturesRead)]
    public async Task<IActionResult> BySubject([FromQuery] string? module, [FromQuery] string? subjectType, [FromQuery] string? subjectId,
        CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new ListSignaturesBySubjectQuery(module, subjectType, subjectId), ct));

    /// <summary>§4.5 getSignature.</summary>
    [HttpGet("{id:guid}")]
    [HasPermission(ESignPermissions.SignaturesRead)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetSignatureQuery(id), ct));

    /// <summary>§4.7 verifySignature — computed, never written back.</summary>
    [HttpPost("{id:guid}/verify")]
    [HasPermission(ESignPermissions.SignaturesVerify)]
    public async Task<IActionResult> Verify(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new VerifySignatureCommand(id), ct));

    /// <summary>§4.8 invalidateSignature — 201 SignatureRecord with its invalidation.</summary>
    [HttpPost("{id:guid}/invalidate")]
    [HasPermission(ESignPermissions.SignaturesInvalidate)]
    public async Task<IActionResult> Invalidate(Guid id, [FromBody] InvalidateSignatureRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new InvalidateSignatureCommand(id, request.Ticket, request.Reason, request.IdempotencyKey), ct));
}

[Route("api/v1/esign/requests")]
public sealed class ESignatureRequestsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public ESignatureRequestsController(IMediator mediator) => _mediator = mediator;

    /// <summary>§4.9 createSignatureRequest (API-only key; consumer modules call it with the user's token).</summary>
    [HttpPost]
    [HasPermission(ESignPermissions.RequestsCreate)]
    public async Task<IActionResult> Create([FromBody] CreateSignatureRequestRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateSignatureRequestCommand(request.Subject?.Module, request.Subject?.SubjectType,
            request.Subject?.SubjectId, request.ActionCode, request.MeaningCode, request.SignerUserId, request.DueAtUtc, request.Note,
            request.IdempotencyKey), ct));

    /// <summary>§4.10 listSignatureRequests — scope=mine.</summary>
    [HttpGet]
    [HasPermission(ESignPermissions.RequestsRead)]
    public async Task<IActionResult> List([FromQuery] string? scope, [FromQuery] string? module, [FromQuery] string? status,
        [FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc, [FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        CancellationToken ct = default) =>
        CreateActionResultInstance(await _mediator.Send(new ListSignatureRequestsQuery(scope, module, status, fromUtc, toUtc, page, pageSize), ct));

    /// <summary>§4.11 getSignatureRequest.</summary>
    [HttpGet("{id:guid}")]
    [HasPermission(ESignPermissions.RequestsRead)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetSignatureRequestQuery(id), ct));

    /// <summary>§4.12 rejectSignatureRequest.</summary>
    [HttpPost("{id:guid}/reject")]
    [HasPermission(ESignPermissions.RequestsReject)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] ESignReasonRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new RejectSignatureRequestCommand(id, request.Reason, request.IdempotencyKey), ct));

    /// <summary>§4.13 cancelSignatureRequest — the requester only.</summary>
    [HttpPost("{id:guid}/cancel")]
    [HasPermission(ESignPermissions.RequestsCreate)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] ESignReasonRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CancelSignatureRequestCommand(id, request.Reason), ct));
}

[Route("api/v1/esign")]
public sealed class ESignatureSettingsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public ESignatureSettingsController(IMediator mediator) => _mediator = mediator;

    /// <summary>§4.14 getPolicy.</summary>
    [HttpGet("policy")]
    [HasPermission(ESignPermissions.PolicyRead)]
    public async Task<IActionResult> GetPolicy([FromQuery] Guid? legalEntityId, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetPolicyQuery(legalEntityId), ct));

    /// <summary>§4.15 getEffectivePolicy — what the signing dialog needs.</summary>
    [HttpGet("policy/effective")]
    [HasPermission(ESignPermissions.SignaturesSign)]
    public async Task<IActionResult> GetEffectivePolicy([FromQuery] string? actionCode, [FromQuery] string? module, [FromQuery] string? subjectType,
        [FromQuery] string? subjectId, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetEffectivePolicyQuery(actionCode, module, subjectType, subjectId), ct));

    /// <summary>§4.16 policy history.</summary>
    [HttpGet("policy/history")]
    [HasPermission(ESignPermissions.PolicyRead)]
    public async Task<IActionResult> GetPolicyHistory([FromQuery] Guid? legalEntityId, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetPolicyHistoryQuery(legalEntityId), ct));

    /// <summary>§4.16 savePolicy (tenant default).</summary>
    [HttpPut("policy/tenant")]
    [HasPermission(ESignPermissions.PolicyManage)]
    public async Task<IActionResult> SaveTenantPolicy([FromBody] SavePolicyRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(ToCommand(null, request), ct));

    /// <summary>§4.16 saveLegalEntityPolicy (company override; the first PUT creates it with expectedVersion null).</summary>
    [HttpPut("policy/legal-entities/{legalEntityId:guid}")]
    [HasPermission(ESignPermissions.PolicyManage)]
    public async Task<IActionResult> SaveLegalEntityPolicy(Guid legalEntityId, [FromBody] SavePolicyRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(ToCommand(legalEntityId, request), ct));

    /// <summary>§4.16 revertLegalEntityPolicy — 204.</summary>
    [HttpPost("policy/legal-entities/{legalEntityId:guid}/revert")]
    [HasPermission(ESignPermissions.PolicyManage)]
    public async Task<IActionResult> RevertLegalEntityPolicy(Guid legalEntityId, [FromBody] ESignReasonRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new RevertLegalEntityPolicyCommand(legalEntityId, request.Reason), ct));

    /// <summary>
    /// §4.20 listMeanings — key <c>esign.meanings.read</c> OR <c>esign.signatures.sign</c> (the dialog lists meanings).
    /// [HasAnyPermission] (WP-MVP1-SEC-ESIGN1) — the either-key rule is declared, so A1 and the guards see both keys.
    /// </summary>
    [HttpGet("meanings")]
    [HasAnyPermission(ESignPermissions.MeaningsRead, ESignPermissions.SignaturesSign)]
    public async Task<IActionResult> ListMeanings([FromQuery] bool activeOnly = false, CancellationToken ct = default) =>
        CreateActionResultInstance(await _mediator.Send(new ListMeaningsQuery(activeOnly), ct));

    /// <summary>§4.21 createMeaning.</summary>
    [HttpPost("meanings")]
    [HasPermission(ESignPermissions.MeaningsManage)]
    public async Task<IActionResult> CreateMeaning([FromBody] CreateMeaningRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateMeaningCommand(request.Code, request.Names, request.Description), ct));

    /// <summary>§4.21 updateMeaning — the code is the path and never changes.</summary>
    [HttpPut("meanings/{code}")]
    [HasPermission(ESignPermissions.MeaningsManage)]
    public async Task<IActionResult> UpdateMeaning(string code, [FromBody] UpdateMeaningRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new UpdateMeaningCommand(code, request.Names, request.Description), ct));

    /// <summary>§4.21 activateMeaning.</summary>
    [HttpPost("meanings/{code}/activate")]
    [HasPermission(ESignPermissions.MeaningsManage)]
    public async Task<IActionResult> ActivateMeaning(string code, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new SetMeaningActiveCommand(code, true), ct));

    /// <summary>§4.21 deactivateMeaning — 409 ESIGN_MEANING_IN_USE while a policy row uses it.</summary>
    [HttpPost("meanings/{code}/deactivate")]
    [HasPermission(ESignPermissions.MeaningsManage)]
    public async Task<IActionResult> DeactivateMeaning(string code, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new SetMeaningActiveCommand(code, false), ct));

    /// <summary>§4.22 listSignableActions.</summary>
    [HttpGet("actions")]
    [HasPermission(ESignPermissions.PolicyRead)]
    public async Task<IActionResult> ListActions(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new ListSignableActionsQuery(), ct));

    private static SavePolicyCommand ToCommand(Guid? legalEntityId, SavePolicyRequest request) => new(
        legalEntityId,
        request.Rows?.Select(r => new PolicyRowInput(r.ActionCode, r.Required, r.MeaningCode, r.VisualMode, r.SecondFactor)).ToList(),
        request.ReauthEvery,
        request.ReauthWindowMinutes,
        request.SampleApprovalRequired,
        request.Reason,
        request.ExpectedVersion);
}

[Route("api/v1/esign/exports")]
public sealed class ESignatureExportsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public ESignatureExportsController(IMediator mediator) => _mediator = mediator;

    /// <summary>§4.25 createExport — the file stream (PDF or CSV) with <c>X-Export-Number</c>; failures use the envelope.</summary>
    [HttpPost]
    [HasPermission(ESignPermissions.SignaturesExport)]
    public async Task<IActionResult> Create([FromBody] CreateExportRequest request, CancellationToken ct)
    {
        var filter = request.Filter;
        var result = await _mediator.Send(new CreateExportCommand(request.Format, filter?.FromUtc, filter?.ToUtc, filter?.Module, filter?.SignerUserId,
            filter?.MeaningCode, filter?.LegalEntityId, request.IdempotencyKey), ct);
        if (!result.IsSuccessful || result.Data is null)
        {
            return CreateActionResultInstance(result);
        }

        Response.Headers["X-Export-Number"] = result.Data.Entry.ExportNumber;
        return File(result.Data.Content, result.Data.MediaType, result.Data.FileName);
    }

    /// <summary>§4.26 listExports.</summary>
    [HttpGet]
    [HasPermission(ESignPermissions.SignaturesExport)]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default) =>
        CreateActionResultInstance(await _mediator.Send(new ListExportsQuery(page, pageSize), ct));
}
