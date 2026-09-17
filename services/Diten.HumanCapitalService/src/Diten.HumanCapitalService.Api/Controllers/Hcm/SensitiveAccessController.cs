using System.Security.Claims;
using Diten.HumanCapitalService.Api.Controllers.Common;
using Diten.HumanCapitalService.Api.Security;
using Diten.HumanCapitalService.Application.Features.SensitiveAccess;
using Diten.HumanCapitalService.Application.Features.SensitiveAccess.Commands;
using Diten.HumanCapitalService.Application.Features.SensitiveAccess.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.HumanCapitalService.Api.Controllers.Hcm;

[Route("api/sensitive-access")]
[Authorize]
public sealed class SensitiveAccessController : CustomBaseController
{
    private readonly IMediator _mediator;

    public SensitiveAccessController(IMediator mediator) => _mediator = mediator;

    [HttpGet("health")]
    [HasPermission("hcm.sensitive-access.read")]
    public async Task<IActionResult> GetHealth(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetSensitiveAccessHealthQuery(), ct));

    [HttpPost("employee-projections/{id:guid}/decisions")]
    [HasPermission("hcm.sensitive-access.read")]
    public async Task<IActionResult> EvaluateEmployeeProjection(
        Guid id,
        [FromBody] SensitiveAccessDecisionRequest request,
        CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluateEmployeeProjectionSensitiveAccessQuery(
            id,
            request,
            EffectivePermissions()), ct));

    [HttpPost("policy/validate")]
    [HasPermission("hcm.sensitive-access.manage")]
    public async Task<IActionResult> ValidatePolicy(
        [FromBody] SensitiveAccessPolicyValidationRequest request,
        CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new ValidateSensitiveAccessPolicyCommand(request), ct));

    [HttpGet("audit/deferred")]
    [HasPermission("hcm.sensitive-access.audit.read")]
    public async Task<IActionResult> GetDeferredAuditStatus(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetSensitiveAccessAuditStatusQuery(), ct));

    private IReadOnlyCollection<string> EffectivePermissions() =>
        User.Claims
            .Where(claim => IsPermissionClaim(claim.Type))
            .SelectMany(claim => SplitPermissionClaim(claim.Value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static bool IsPermissionClaim(string type) =>
        string.Equals(type, "permission", StringComparison.OrdinalIgnoreCase)
        || string.Equals(type, "permissions", StringComparison.OrdinalIgnoreCase)
        || string.Equals(type, "scope", StringComparison.OrdinalIgnoreCase)
        || string.Equals(type, ClaimTypes.Role, StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> SplitPermissionClaim(string value) =>
        value.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
