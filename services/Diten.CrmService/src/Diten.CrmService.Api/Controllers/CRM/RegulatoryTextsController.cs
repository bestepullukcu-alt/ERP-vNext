using Diten.CrmService.Api.Models.CRM;
using Diten.CrmService.Application.Features.Knowledge.Regulatory;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LegalPerms = Diten.CrmService.Application.Features.Knowledge.Regulatory.CountryLegalProfilePermissions;
using SafetyPerms = Diten.CrmService.Application.Features.Knowledge.Regulatory.SafetyTextPermissions;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>
/// WP-KP-5a — the two Regulatory-approved master texts of the page designer's locked blocks: safety texts (product ×
/// country × language) under <c>/api/crm/knowledge/safety-texts</c> and country legal profiles (country × language) under
/// <c>/api/crm/knowledge/country-legal-profiles</c>. Authoring is <c>manage</c>, sending for approval / withdrawing is
/// <c>submit</c>; a decision needs only <c>read</c> here — MOD-0023 decides who may act on the task (its candidates)
/// and CRM enforces the person SoD. No DELETE (archive).
/// </summary>
[Authorize]
public sealed class RegulatoryTextsController : CustomBaseController
{
    private const string Safety = "api/crm/knowledge/safety-texts";
    private const string Legal = "api/crm/knowledge/country-legal-profiles";

    private readonly IMediator _mediator;

    public RegulatoryTextsController(IMediator mediator) => _mediator = mediator;

    // ---------------- safety texts ----------------

    [HttpGet(Safety)]
    [HasPermission(SafetyPerms.Read)]
    public async Task<IActionResult> ListSafetyTexts(
        [FromQuery] Guid? productId, [FromQuery] string? countryCode, [FromQuery] string? languageCode,
        [FromQuery] string? status, [FromQuery] bool includeArchived = false, CancellationToken cancellationToken = default)
        => CreateActionResultInstance(await _mediator.Send(
            new ListSafetyTextsQuery(productId, countryCode, languageCode, status, includeArchived), cancellationToken));

    [HttpGet(Safety + "/resolve")]
    [HasPermission(SafetyPerms.Read)]
    public async Task<IActionResult> ResolveSafetyText(
        [FromQuery] Guid productId, [FromQuery] string? countryCode, [FromQuery] string? languageCode,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new ResolveSafetyTextQuery(productId, countryCode, languageCode), cancellationToken));

    [HttpGet(Safety + "/{id:guid}")]
    [HasPermission(SafetyPerms.Read)]
    public async Task<IActionResult> GetSafetyText(Guid id, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetSafetyTextQuery(id), cancellationToken));

    [HttpPost(Safety)]
    [HasPermission(SafetyPerms.Manage)]
    public async Task<IActionResult> CreateSafetyText([FromBody] CreateSafetyTextRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new CreateSafetyTextCommand(
            request.GlobalProductId, request.GlobalProductCodeDisplay, request.CountryCode, request.LanguageCode, request.Body,
            request.ShortBody, request.SourceDocumentRef, request.SourceDate, request.ApprovalReference), cancellationToken));

    [HttpPut(Safety + "/{id:guid}")]
    [HasPermission(SafetyPerms.Manage)]
    public async Task<IActionResult> UpdateSafetyText(
        Guid id, [FromBody] UpdateSafetyTextRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new UpdateSafetyTextCommand(
            id, request.Body, request.ShortBody, request.SourceDocumentRef, request.SourceDate, request.ApprovalReference,
            request.ExpectedVersion), cancellationToken));

    [HttpPost(Safety + "/{id:guid}/new-version")]
    [HasPermission(SafetyPerms.Manage)]
    public async Task<IActionResult> NewSafetyTextVersion(Guid id, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new NewSafetyTextVersionCommand(id), cancellationToken));

    [HttpPost(Safety + "/{id:guid}/submit")]
    [HasPermission(SafetyPerms.Submit)]
    public async Task<IActionResult> SubmitSafetyText(Guid id, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new SubmitSafetyTextCommand(id), cancellationToken));

    [HttpPost(Safety + "/{id:guid}/withdraw")]
    [HasPermission(SafetyPerms.Submit)]
    public async Task<IActionResult> WithdrawSafetyText(Guid id, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new WithdrawSafetyTextCommand(id), cancellationToken));

    [HttpPost(Safety + "/{id:guid}/decision")]
    [HasPermission(SafetyPerms.Read)]
    public async Task<IActionResult> DecideSafetyText(
        Guid id, [FromBody] RegulatoryTextDecisionRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new DecideSafetyTextCommand(id, request.Outcome, request.Comment), cancellationToken));

    [HttpPost(Safety + "/{id:guid}/archive")]
    [HasPermission(SafetyPerms.Manage)]
    public async Task<IActionResult> ArchiveSafetyText(Guid id, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new ArchiveSafetyTextCommand(id), cancellationToken));

    // ---------------- country legal profiles ----------------

    [HttpGet(Legal)]
    [HasPermission(LegalPerms.Read)]
    public async Task<IActionResult> ListLegalProfiles(
        [FromQuery] string? countryCode, [FromQuery] string? languageCode, [FromQuery] string? status,
        [FromQuery] bool includeArchived = false, CancellationToken cancellationToken = default)
        => CreateActionResultInstance(await _mediator.Send(
            new ListCountryLegalProfilesQuery(countryCode, languageCode, status, includeArchived), cancellationToken));

    [HttpGet(Legal + "/resolve")]
    [HasPermission(LegalPerms.Read)]
    public async Task<IActionResult> ResolveLegalProfile(
        [FromQuery] string? countryCode, [FromQuery] string? languageCode, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new ResolveCountryLegalProfileQuery(countryCode, languageCode), cancellationToken));

    [HttpGet(Legal + "/{id:guid}")]
    [HasPermission(LegalPerms.Read)]
    public async Task<IActionResult> GetLegalProfile(Guid id, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetCountryLegalProfileQuery(id), cancellationToken));

    [HttpPost(Legal)]
    [HasPermission(LegalPerms.Manage)]
    public async Task<IActionResult> CreateLegalProfile(
        [FromBody] CreateCountryLegalProfileRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new CreateCountryLegalProfileCommand(
            request.CountryCode, request.LanguageCode, request.LegalFooterText, request.MarketingAuthorizationHolder,
            request.AdverseEventReportingText, request.PromotionalNotice, request.PageApprovalCodeFormat), cancellationToken));

    [HttpPut(Legal + "/{id:guid}")]
    [HasPermission(LegalPerms.Manage)]
    public async Task<IActionResult> UpdateLegalProfile(
        Guid id, [FromBody] UpdateCountryLegalProfileRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new UpdateCountryLegalProfileCommand(
            id, request.LegalFooterText, request.MarketingAuthorizationHolder, request.AdverseEventReportingText,
            request.PromotionalNotice, request.PageApprovalCodeFormat, request.ExpectedVersion), cancellationToken));

    [HttpPost(Legal + "/{id:guid}/new-version")]
    [HasPermission(LegalPerms.Manage)]
    public async Task<IActionResult> NewLegalProfileVersion(Guid id, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new NewCountryLegalProfileVersionCommand(id), cancellationToken));

    [HttpPost(Legal + "/{id:guid}/submit")]
    [HasPermission(LegalPerms.Submit)]
    public async Task<IActionResult> SubmitLegalProfile(Guid id, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new SubmitCountryLegalProfileCommand(id), cancellationToken));

    [HttpPost(Legal + "/{id:guid}/withdraw")]
    [HasPermission(LegalPerms.Submit)]
    public async Task<IActionResult> WithdrawLegalProfile(Guid id, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new WithdrawCountryLegalProfileCommand(id), cancellationToken));

    [HttpPost(Legal + "/{id:guid}/decision")]
    [HasPermission(LegalPerms.Read)]
    public async Task<IActionResult> DecideLegalProfile(
        Guid id, [FromBody] RegulatoryTextDecisionRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new DecideCountryLegalProfileCommand(id, request.Outcome, request.Comment), cancellationToken));

    [HttpPost(Legal + "/{id:guid}/archive")]
    [HasPermission(LegalPerms.Manage)]
    public async Task<IActionResult> ArchiveLegalProfile(Guid id, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new ArchiveCountryLegalProfileCommand(id), cancellationToken));
}
