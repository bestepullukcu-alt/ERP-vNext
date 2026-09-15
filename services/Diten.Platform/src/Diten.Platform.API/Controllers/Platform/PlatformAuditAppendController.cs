using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.Audit;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Platform;

/// <summary>
/// MOD-0021 governed TENANT audit append. Other services (HCM, CRM) call it over the Gateway with the user's own
/// token and X-Tenant-Id.
///
/// <para><b>⚠ BL-421 — THE ACTOR IS THE AUTHENTICATED CALLER, NEVER THE BODY.</b> The tenant was already forced to the
/// caller's, but ActorType and ActorId were taken from the request body, so anyone holding
/// <c>platform.audit.events.append</c> could write events into their tenant's trail naming a platform administrator,
/// the system, a service or another user. The record now names the caller's user id, with the actor type resolved by
/// <see cref="AuditActorTypeResolver.ForCommand"/> (BL-409, the one resolver). The body may still carry the actor
/// fields — both known callers send them — but a value naming anyone other than the caller is refused 400
/// (<c>actor_type_mismatch</c> / <c>actor_id_mismatch</c>) rather than silently rewritten, so a caller that sends the
/// wrong actor finds out. A principal the record cannot name is refused 403 (<c>actor_unresolved</c>); the audit
/// service would refuse an Unknown actor anyway, and this makes that refusal explicit instead of a dropped event.</para>
///
/// <para>The X-Internal-Api-Key S2S sink (<c>InternalAuditController</c>, <c>/api/internal/audit/append</c>) is a
/// different trust boundary — a keyed service vouches for the actor it forwards — and is deliberately unchanged.</para>
/// </summary>
[ApiController]
[Route("api/v1/platform/audit/events")]
[Authorize]
public sealed class PlatformAuditAppendController : CustomBaseController
{
    public const string AppendPermission = "platform.audit.events.append";

    private readonly IAuditService _auditService;
    private readonly ITenantContext _tenantContext;
    private readonly ITenantAuthorizationContext _principal;

    public PlatformAuditAppendController(
        IAuditService auditService,
        ITenantContext tenantContext,
        ITenantAuthorizationContext principal)
    {
        _auditService = auditService;
        _tenantContext = tenantContext;
        _principal = principal;
    }

    [HttpPost]
    [HasPermission(AppendPermission)]
    public async Task<IActionResult> Append(
        [FromBody] GovernedAuditAppendRequest request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.IsResolved || _tenantContext.TenantId == Guid.Empty)
        {
            return CreateActionResultInstance(
                Response<GovernedAuditAppendResponse>.Fail("tenant_context_required", 400));
        }

        // BL-421 — who is appending is settled before what they sent: a caller the record cannot name is refused.
        var callerActorType = AuditActorTypeResolver.ForCommand(_principal);
        if (!_principal.IsAuthenticated
            || callerActorType == AuditActorType.Unknown
            || _principal.UserId == Guid.Empty)
        {
            return CreateActionResultInstance(
                Response<GovernedAuditAppendResponse>.Fail(GovernedAuditAppendValidation.ActorUnresolved, 403));
        }

        var tenantId = _tenantContext.TenantId;
        var validationErrors = GovernedAuditAppendValidation.Validate(request, tenantId)
            .Concat(GovernedAuditAppendValidation.ValidateActor(request, callerActorType, _principal.UserId))
            .ToList();
        if (validationErrors.Count > 0)
        {
            var status = validationErrors.Contains("target_tenant_mismatch", StringComparer.Ordinal)
                ? 403
                : 400;
            return CreateActionResultInstance(
                Response<GovernedAuditAppendResponse>.Fail(validationErrors, status));
        }

        GovernedAuditAppendValidation.TryParseCategory(request.Category, out var category);
        GovernedAuditAppendValidation.TryParseOperation(request.Operation, out var operation);
        GovernedAuditAppendValidation.TryParseOutcome(request.Outcome, out var outcome);

        var result = await _auditService.AppendAsync(new AuditAppendRequest
        {
            CorrelationId = request.CorrelationId,
            RequestType = request.RequestType.Trim(),
            ActorType = callerActorType,
            ActorId = _principal.UserId,
            TargetTenantId = tenantId,
            Category = category,
            EntityType = request.EntityType.Trim(),
            EntityId = request.EntityId,
            Operation = operation,
            Outcome = outcome,
            Metadata = request.Metadata,
            OccurredAtUtc = request.OccurredAtUtc,
            SourceService = request.SourceService.Trim(),
            SourceModule = string.IsNullOrWhiteSpace(request.SourceModule) ? null : request.SourceModule.Trim(),
            Sequence = request.Sequence,
            IsPlatformGlobal = false
        }, cancellationToken);

        var response = GovernedAuditAppendValidation.ToResponse(result);
        return CreateActionResultInstance(
            Response<GovernedAuditAppendResponse>.Success(response, ToStatusCode(result)));
    }

    private static int ToStatusCode(AuditAppendResult result)
        => result.Status switch
        {
            AuditAppendStatus.Queued => 201,
            AuditAppendStatus.Duplicate => 200,
            AuditAppendStatus.EnqueueFailed => 503,
            AuditAppendStatus.SkippedRecursion => 409,
            AuditAppendStatus.Rejected => result.ShouldBreakBusinessCommand ? 424 : 400,
            _ => 500
        };
}
