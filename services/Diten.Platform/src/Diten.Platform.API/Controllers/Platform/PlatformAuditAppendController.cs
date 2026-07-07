using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.Audit;
using Diten.Platform.Common.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Platform;

[ApiController]
[Route("api/v1/platform/audit/events")]
[Authorize]
public sealed class PlatformAuditAppendController : CustomBaseController
{
    public const string AppendPermission = "platform.audit.events.append";

    private readonly IAuditService _auditService;
    private readonly ITenantContext _tenantContext;

    public PlatformAuditAppendController(
        IAuditService auditService,
        ITenantContext tenantContext)
    {
        _auditService = auditService;
        _tenantContext = tenantContext;
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

        var tenantId = _tenantContext.TenantId;
        var validationErrors = GovernedAuditAppendValidation.Validate(request, tenantId);
        if (validationErrors.Count > 0)
        {
            var status = validationErrors.Contains("target_tenant_mismatch", StringComparer.Ordinal)
                ? 403
                : 400;
            return CreateActionResultInstance(
                Response<GovernedAuditAppendResponse>.Fail(validationErrors, status));
        }

        GovernedAuditAppendValidation.TryParseActorType(request.ActorType, out var actorType);
        GovernedAuditAppendValidation.TryParseCategory(request.Category, out var category);
        GovernedAuditAppendValidation.TryParseOperation(request.Operation, out var operation);
        GovernedAuditAppendValidation.TryParseOutcome(request.Outcome, out var outcome);

        var result = await _auditService.AppendAsync(new AuditAppendRequest
        {
            CorrelationId = request.CorrelationId,
            RequestType = request.RequestType.Trim(),
            ActorType = actorType,
            ActorId = request.ActorId,
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
