using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Diten.PlanningService.Application.Features.DemandPlanning.HistoryImports;

namespace Diten.PlanningService.Api.Features.DemandPlanning;

// Authorization runs before MVC. Record attributable permission failures here.
public sealed class DemandReviewAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly IHistoryImportReviewAuditStore _audit;
    private readonly AuthorizationMiddlewareResultHandler _defaultHandler = new();

    public DemandReviewAuthorizationResultHandler(IHistoryImportReviewAuditStore audit) => _audit = audit;

    public async Task HandleAsync(RequestDelegate next, HttpContext context,
        AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden && HttpMethods.IsPost(context.Request.Method) &&
            context.Request.Path.Value?.EndsWith("/review", StringComparison.OrdinalIgnoreCase) == true &&
            context.Request.Path.StartsWithSegments("/api/v2/demand/history-import-batches") &&
            Guid.TryParse(context.Request.RouteValues["batchId"]?.ToString(), out var batchId) &&
            context.Items["DemandPlanning.TenantId"] is Guid tenantId &&
            Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                context.User.FindFirstValue("sub"), out var actorId))
        {
            try
            {
                await _audit.AppendOnceAsync(new HistoryImportReviewFailure(
                    tenantId, Guid.Empty, batchId, actorId,
                    context.Request.Headers["Idempotency-Key"].ToString(), string.Empty,
                    "PermissionDenied", "Required review permission not granted.",
                    DateTimeOffset.UtcNow), context.RequestAborted);
            }
            catch (Exception ex) when (ex is not OperationCanceledException ||
                                       !context.RequestAborted.IsCancellationRequested)
            {
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                return;
            }
        }
        await _defaultHandler.HandleAsync(next, context, policy, authorizeResult);
    }
}
