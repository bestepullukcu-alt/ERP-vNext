using System.Security.Claims;
using Diten.HcmService.Application.Features.CoreHrEmployeeMaster.Queries;
using Diten.HcmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.HcmService.Api.Controllers.Hcm;

[Authorize]
[ApiController]
[Route("api/v1/hcm/employees")]
public sealed class EmployeesController : CustomBaseController
{
    private const string SearchPermission = "mod0251.employee.search";
    private const string ViewPermission = "mod0251.employee.view";
    private readonly IMediator _mediator;

    public EmployeesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HasPermission(SearchPermission)]
    public async Task<IActionResult> Search(
        [FromQuery] string? search,
        [FromQuery] string? employeeStatus,
        [FromQuery] string? workerType,
        [FromQuery] string? employmentType,
        [FromQuery] Guid? legalEntityId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = null,
        CancellationToken cancellationToken = default)
    {
        var response = await _mediator.Send(
            new SearchEmployeesQuery(
                search,
                employeeStatus,
                workerType,
                employmentType,
                legalEntityId,
                page,
                pageSize,
                sortBy,
                sortDirection,
                BuildActionPermissions(User)),
            cancellationToken);

        return CreateActionResultInstance(response);
    }

    [HttpGet("{employeeId:guid}")]
    [HasPermission(ViewPermission)]
    public async Task<IActionResult> Get(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var response = await _mediator.Send(new GetEmployeeQuery(employeeId), cancellationToken);
        return CreateActionResultInstance(response);
    }

    private static EmployeeRegistryActionPermissions BuildActionPermissions(ClaimsPrincipal user)
        => new(
            HasPermission(user, "mod0251.employee.view"),
            HasPermission(user, "mod0251.employee.edit_legal"),
            HasPermission(user, "mod0251.employee.edit_employment"),
            HasPermission(user, "mod0251.employee.change_status"),
            HasPermission(user, "mod0251.employee.attach_evidence"),
            HasPermission(user, "mod0251.employee.export"));

    private static bool HasPermission(ClaimsPrincipal user, string permission)
    {
        return user.Claims.Any(claim =>
            IsPermissionClaim(claim.Type)
            && string.Equals(claim.Value, permission, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsPermissionClaim(string claimType)
        => string.Equals(claimType, "permission", StringComparison.OrdinalIgnoreCase)
            || string.Equals(claimType, "permissions", StringComparison.OrdinalIgnoreCase);
}
