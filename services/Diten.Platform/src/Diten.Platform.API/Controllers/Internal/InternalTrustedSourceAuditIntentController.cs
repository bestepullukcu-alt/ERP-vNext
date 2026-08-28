using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Internal;

[ApiController]
[AllowAnonymous]
[Route("api/internal/v1/audit/source-intents")]
public sealed class InternalTrustedSourceAuditIntentController : CustomBaseController
{
    private readonly ITrustedSourceAuditIntentRequestExecutor _requestExecutor;

    public InternalTrustedSourceAuditIntentController(ITrustedSourceAuditIntentRequestExecutor requestExecutor)
    {
        _requestExecutor = requestExecutor;
    }

    [HttpPost("accept")]
    public Task<IActionResult> Accept(CancellationToken cancellationToken) =>
        _requestExecutor.ExecuteAsync(HttpContext, cancellationToken, ResolveResult, ResolveFailure);

    private IActionResult ResolveResult(TrustedSourceAuditIntentAcceptanceResult result)
    {
        var correlationId = HttpContext.TraceIdentifier;
        return result.Status switch
        {
            TrustedSourceAuditIntentAcceptanceResult.AcceptanceStatus.Accepted =>
                CreateActionResultInstance(Response<TrustedSourceAuditIntentAcceptanceReceipt>.Success(
                    result.Receipt!, StatusCodes.Status201Created, correlationId)),
            TrustedSourceAuditIntentAcceptanceResult.AcceptanceStatus.Duplicate =>
                CreateActionResultInstance(Response<TrustedSourceAuditIntentAcceptanceReceipt>.Success(
                    result.Receipt!, StatusCodes.Status200OK, correlationId)),
            TrustedSourceAuditIntentAcceptanceResult.AcceptanceStatus.Invalid =>
                ResolveFailure(StatusCodes.Status400BadRequest, result.ErrorCode ?? "AUDIT_SOURCE_INTENT_INVALID"),
            TrustedSourceAuditIntentAcceptanceResult.AcceptanceStatus.ContractUnsupported or
                TrustedSourceAuditIntentAcceptanceResult.AcceptanceStatus.MappingUnsupported or
                TrustedSourceAuditIntentAcceptanceResult.AcceptanceStatus.IdempotencyConflict =>
                ResolveFailure(StatusCodes.Status409Conflict, result.ErrorCode ?? "AUDIT_SOURCE_INTENT_IDEMPOTENCY_CONFLICT"),
            TrustedSourceAuditIntentAcceptanceResult.AcceptanceStatus.Unavailable =>
                ResolveFailure(StatusCodes.Status503ServiceUnavailable, result.ErrorCode ?? "AUDIT_SOURCE_INTENT_UNAVAILABLE"),
            _ => ResolveFailure(StatusCodes.Status503ServiceUnavailable, "AUDIT_SOURCE_INTENT_UNAVAILABLE")
        };
    }

    private IActionResult ResolveFailure(int statusCode, string code) => CreateActionResultInstance(
        Response<TrustedSourceAuditIntentAcceptanceReceipt>.Fail(
            code,
            statusCode,
            code,
            HttpContext.TraceIdentifier));
}
