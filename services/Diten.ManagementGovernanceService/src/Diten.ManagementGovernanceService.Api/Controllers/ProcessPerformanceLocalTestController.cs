using System.Security.Claims;
using Diten.ManagementGovernanceService.Api.LocalTestSecurity;
using Diten.ManagementGovernanceService.Application.Modules.ProcessModeling.PerformanceImprovement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DwsEnvelope = Diten.ManagementGovernanceService.Application.Features.Dws;

namespace Diten.ManagementGovernanceService.Api.Controllers;

[ApiController]
[Route("internal/local-test/v1/process-modeling")]
[Authorize(AuthenticationSchemes = LocalTestAuthenticationHandler.SchemeName)]
public sealed class ProcessPerformanceLocalTestController(ProcessKpiBindingDependencyGate dependencyGate)
    : CustomBaseController
{
    [HttpGet("kpi-bindings")]
    public IActionResult ListBindings() => Unavailable(PerformanceImprovementPermissions.KpiRead);

    [HttpGet("kpi-bindings/{id:guid}")]
    public IActionResult GetBinding(Guid id)
    {
        if (id == Guid.Empty) return Failure("process_performance_bad_request", StatusCodes.Status400BadRequest);
        return Unavailable(PerformanceImprovementPermissions.KpiRead);
    }

    [HttpPost("kpi-bindings")]
    public Task<IActionResult> CreateBinding(CancellationToken cancellationToken) =>
        RejectBodyOrReturnUnavailableAsync(PerformanceImprovementPermissions.KpiCreate, cancellationToken);

    [HttpPost("kpi-bindings/{id:guid}/archive")]
    public async Task<IActionResult> ArchiveBinding(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return Failure("process_performance_bad_request", StatusCodes.Status400BadRequest);
        return await RejectBodyOrReturnUnavailableAsync(
            PerformanceImprovementPermissions.KpiArchive,
            cancellationToken);
    }

    [HttpGet("performance-observations")]
    public IActionResult ListPerformanceObservations() => Unavailable(PerformanceImprovementPermissions.KpiRead);

    private IActionResult Unavailable(string permission)
    {
        var contextError = ValidateTrustedContext(permission);
        if (contextError is not null) return contextError;

        var result = dependencyGate.Evaluate(permission);
        return Failure(result.Code, result.StatusCode);
    }

    private async Task<IActionResult> RejectBodyOrReturnUnavailableAsync(
        string permission,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body, leaveOpen: true);
        var body = await reader.ReadToEndAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(body))
            return Failure("process_performance_bad_request", StatusCodes.Status400BadRequest);

        return Unavailable(permission);
    }

    private IActionResult? ValidateTrustedContext(string permission)
    {
        if (!Guid.TryParse(User.FindFirstValue("tenant_id"), out var tenantId) || tenantId == Guid.Empty ||
            !Guid.TryParse(User.FindFirstValue("sub"), out var actorId) || actorId == Guid.Empty)
            return Failure("process_performance_unauthenticated", StatusCodes.Status401Unauthorized);

        if (!Request.Headers.TryGetValue("X-Tenant-Id", out var tenantHeader) || tenantHeader.Count != 1 ||
            !Guid.TryParse(tenantHeader[0], out var headerTenantId) || headerTenantId != tenantId)
            return Failure("process_performance_bad_request", StatusCodes.Status400BadRequest);

        if (!User.FindAll("permission").Any(claim => string.Equals(claim.Value, permission, StringComparison.Ordinal)))
            return Failure("process_performance_permission_denied", StatusCodes.Status403Forbidden);

        return null;
    }

    private IActionResult Failure(string code, int statusCode) =>
        FromResponse(DwsEnvelope.Response<object>.Fail(code, statusCode));
}
