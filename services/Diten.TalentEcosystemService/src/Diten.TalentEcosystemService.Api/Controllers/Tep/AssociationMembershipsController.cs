using Diten.TalentEcosystemService.Api.Controllers.Common;
using Diten.TalentEcosystemService.Api.Security;
using Diten.TalentEcosystemService.Application.Features.AssociationMemberships;
using Diten.TalentEcosystemService.Application.Features.AssociationMemberships.Commands;
using Diten.TalentEcosystemService.Application.Features.AssociationMemberships.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.TalentEcosystemService.Api.Controllers.Tep;

[Route("api/tep-association-memberships")]
[Authorize]
public sealed class AssociationMembershipsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public AssociationMembershipsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(AssociationMembershipPermissions.Read)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetAssociationMembershipListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(AssociationMembershipPermissions.Read)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetAssociationMembershipByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(AssociationMembershipPermissions.Manage)]
    public async Task<IActionResult> Create([FromBody] AssociationMembershipRegistryRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateAssociationMembershipCommand(request), ct));

    [HttpPut("{id:guid}")]
    [HasPermission(AssociationMembershipPermissions.Manage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] AssociationMembershipRegistryRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new UpdateAssociationMembershipCommand(id, request), ct));

    [HttpPatch("{id:guid}/archive")]
    [HasPermission(AssociationMembershipPermissions.Archive)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new ArchiveAssociationMembershipCommand(id), ct));

    [HttpPost("{id:guid}/evaluate")]
    [HasPermission(AssociationMembershipPermissions.Evaluate)]
    public async Task<IActionResult> Evaluate(Guid id, [FromBody] EvaluateAssociationMembershipRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluateAssociationMembershipCommand(id, request), ct));

    [HttpPatch("{id:guid}/member-company")]
    [HasPermission(AssociationMembershipPermissions.MemberCompanyManage)]
    public async Task<IActionResult> UpdateMemberCompany(Guid id, [FromBody] AssociationMemberCompanyRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new UpdateAssociationMemberCompanyCommand(id, request), ct));
}
