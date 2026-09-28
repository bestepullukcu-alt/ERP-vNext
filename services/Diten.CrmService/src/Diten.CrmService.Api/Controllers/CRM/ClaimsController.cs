using Diten.CrmService.Api.Models.CRM;
using Diten.CrmService.Application.Features.ContentComposition.Claims;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Perms = Diten.CrmService.Application.Features.ContentComposition.Claims.ClaimPermissions;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>
/// SCMM-12-API (CAND-CAP-0011) — Claim authoring HTTP surface. Canonical under
/// <c>/api/crm/content-composition/claims</c>. A thin adapter over the ready Claim CQRS (SCMM-12): every action just
/// maps the request onto the command/query and dispatches through MediatR — no business logic lives here. Approval is a
/// dedicated action (draft → approved); there is <b>no delete endpoint</b> — closing a claim is Archive.
/// </summary>
[Authorize]
public sealed class ClaimsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public ClaimsController(IMediator mediator) => _mediator = mediator;

    [HttpGet("api/crm/content-composition/claims")]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery] DateTimeOffset? effectiveAt,
        [FromQuery] string? search,
        [FromQuery] bool includeArchived = true,
        CancellationToken cancellationToken = default)
        => CreateActionResultInstance(await _mediator.Send(
            new ListClaimsQuery(status, effectiveAt, search, includeArchived), cancellationToken));

    [HttpGet("api/crm/content-composition/claims/{claimId:guid}")]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> Get(Guid claimId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetClaimQuery(claimId), cancellationToken));

    [HttpPost("api/crm/content-composition/claims")]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> Create(
        [FromBody] CreateClaimRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new CreateClaimCommand(
                request.ClaimCode, request.ClaimName, request.ClaimText, request.EffectiveFrom, request.Description,
                request.Qualifiers, ToApplicabilityInput(request.Applicability), request.EvidenceRefs,
                request.ComponentRefs, request.ClaimVersion, request.Status, request.EffectiveTo,
                request.Kind, request.LocalCountryCode, request.ProductId, request.ProductDisplay,
                request.AudienceProfileIds, request.ResponsibleOrgUnitId, request.TextLanguageCode),
            cancellationToken));

    [HttpPut("api/crm/content-composition/claims/{claimId:guid}")]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> Update(
        Guid claimId, [FromBody] UpdateClaimRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new UpdateClaimCommand(
                claimId, request.ClaimName, request.ClaimText, request.EffectiveFrom, request.Description,
                request.Qualifiers, ToApplicabilityInput(request.Applicability), request.EvidenceRefs,
                request.ComponentRefs, request.ClaimVersion, request.Status, request.EffectiveTo,
                request.ProductId, request.ProductDisplay, request.AudienceProfileIds, request.ResponsibleOrgUnitId,
                request.TextLanguageCode),
            cancellationToken));

    // WP-CL-BE-4 — approval is ONLY the outcome of a MOD-0023 workflow round (submit-review). The route stays so an old
    // client gets an explicit answer instead of a 404.
    [HttpPost("api/crm/content-composition/claims/{claimId:guid}/approve")]
    [HasPermission(Perms.Approve)]
    public IActionResult Approve(Guid claimId)
        => CreateActionResultInstance(ClaimReviewRules.ApprovalViaWorkflowOnly<bool>());

    [HttpPost("api/crm/content-composition/claims/{claimId:guid}/archive")]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> Archive(Guid claimId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new ArchiveClaimCommand(claimId), cancellationToken));

    // ------------------------------------------------------------ WP-CL-BE-1 (claims v2)
    // Same /claims prefix (covered by the gateway's /api/crm/content-composition/{everything} route). Literal segments
    // (coverage, country-versions) never collide with the {claimId:guid} constraint. Existing permission keys only.

    [HttpGet("api/crm/content-composition/claims/coverage")]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> Coverage(
        [FromQuery] Guid? productId,
        [FromQuery] string? kind,
        [FromQuery] string? status,
        [FromQuery] string? search,
        CancellationToken cancellationToken = default)
        => CreateActionResultInstance(await _mediator.Send(
            new GetClaimCoverageQuery(productId, kind, status, search), cancellationToken));

    [HttpPost("api/crm/content-composition/claims/{claimId:guid}/new-version")]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> NewVersion(Guid claimId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new CreateClaimNewVersionCommand(claimId), cancellationToken));

    [HttpPost("api/crm/content-composition/claims/{claimId:guid}/country-closures")]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> CloseCountry(
        Guid claimId, [FromBody] CloseClaimCountryRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new CloseClaimCountryCommand(claimId, request.CountryCode, request.ReasonCode), cancellationToken));

    [HttpPost("api/crm/content-composition/claims/{claimId:guid}/country-closures/{countryCode}/reopen")]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> ReopenCountry(
        Guid claimId, string countryCode, [FromBody] ReopenClaimCountryRequest? request,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new ReopenClaimCountryCommand(claimId, countryCode, request?.Note), cancellationToken));

    [HttpGet("api/crm/content-composition/claims/{claimId:guid}/country-versions")]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> ListCountryVersions(
        Guid claimId,
        [FromQuery] string? countryCode,
        [FromQuery] bool includeArchived = true,
        CancellationToken cancellationToken = default)
        => CreateActionResultInstance(await _mediator.Send(
            new ListClaimCountryVersionsQuery(claimId, countryCode, includeArchived), cancellationToken));

    [HttpPost("api/crm/content-composition/claims/{claimId:guid}/country-versions")]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> CreateCountryVersion(
        Guid claimId, [FromBody] CreateClaimCountryVersionRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new CreateClaimCountryVersionCommand(
                claimId, request.CountryCode, ToTexts(request.Texts), ToTexts(request.Qualifiers),
                request.AdaptationTypeCode, request.AdaptationReason, request.AudienceProfileIds,
                request.ValidFrom, request.ValidTo),
            cancellationToken));

    [HttpGet("api/crm/content-composition/claims/country-versions/{countryVersionId:guid}")]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> GetCountryVersion(Guid countryVersionId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new GetClaimCountryVersionQuery(countryVersionId), cancellationToken));

    [HttpPut("api/crm/content-composition/claims/country-versions/{countryVersionId:guid}")]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> UpdateCountryVersion(
        Guid countryVersionId, [FromBody] UpdateClaimCountryVersionRequest request,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new UpdateClaimCountryVersionCommand(
                countryVersionId, ToTexts(request.Texts), ToTexts(request.Qualifiers), request.AdaptationTypeCode,
                request.AdaptationReason, request.AudienceProfileIds, request.ValidFrom, request.ValidTo),
            cancellationToken));

    [HttpPost("api/crm/content-composition/claims/country-versions/{countryVersionId:guid}/new-version")]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> NewCountryVersion(Guid countryVersionId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new CreateClaimCountryNewVersionCommand(countryVersionId), cancellationToken));

    // WP-CL-BE-4 — see Approve: a country version is approved only through its MOD-0023 workflow round.
    [HttpPost("api/crm/content-composition/claims/country-versions/{countryVersionId:guid}/approve")]
    [HasPermission(Perms.Approve)]
    public IActionResult ApproveCountryVersion(Guid countryVersionId)
        => CreateActionResultInstance(ClaimReviewRules.ApprovalViaWorkflowOnly<bool>());

    // ---- WP-CL-BE-4 — approval rounds (MOD-0023). Submit/withdraw run with the CALLER's token (SoD).

    [HttpPost("api/crm/content-composition/claims/{claimId:guid}/submit-review")]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> SubmitReview(Guid claimId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new SubmitClaimReviewCommand(claimId), cancellationToken));

    [HttpPost("api/crm/content-composition/claims/{claimId:guid}/withdraw-review")]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> WithdrawReview(Guid claimId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new WithdrawClaimReviewCommand(claimId), cancellationToken));

    [HttpGet("api/crm/content-composition/claims/{claimId:guid}/review-history")]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> ReviewHistory(Guid claimId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetClaimReviewHistoryQuery(claimId), cancellationToken));

    [HttpPost("api/crm/content-composition/claims/country-versions/{countryVersionId:guid}/submit-review")]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> SubmitCountryVersionReview(Guid countryVersionId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new SubmitClaimCountryVersionReviewCommand(countryVersionId), cancellationToken));

    [HttpPost("api/crm/content-composition/claims/country-versions/{countryVersionId:guid}/withdraw-review")]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> WithdrawCountryVersionReview(Guid countryVersionId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new WithdrawClaimCountryVersionReviewCommand(countryVersionId), cancellationToken));

    [HttpGet("api/crm/content-composition/claims/country-versions/{countryVersionId:guid}/review-history")]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> CountryVersionReviewHistory(Guid countryVersionId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new GetClaimCountryVersionReviewHistoryQuery(countryVersionId), cancellationToken));

    [HttpPost("api/crm/content-composition/claims/country-versions/{countryVersionId:guid}/archive")]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> ArchiveCountryVersion(Guid countryVersionId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new ArchiveClaimCountryVersionCommand(countryVersionId), cancellationToken));

    private static IReadOnlyList<ClaimLocalizedTextInput>? ToTexts(IReadOnlyList<ClaimLocalizedTextRequest>? texts)
        => texts?.Select(t => new ClaimLocalizedTextInput(t.LanguageCode, t.Text)).ToList();

    // Maps the API applicability request shape onto the application command input. Null stays null.
    private static ClaimApplicabilityInput? ToApplicabilityInput(ClaimApplicabilityRequest? applicability)
        => applicability is null
            ? null
            : new ClaimApplicabilityInput(
                applicability.ProductRefs, applicability.MarketRefs, applicability.AudienceRefs,
                applicability.EligibilityPolicyId);
}
