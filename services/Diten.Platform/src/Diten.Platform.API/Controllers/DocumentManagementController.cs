using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Observability;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.DocumentManagementContract;
using Diten.Platform.Application.Features.DocumentManagementContract.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers;

[ApiController]
[Route(DocumentManagementRoutes.ApiFamily)]
[Authorize]
public sealed class DocumentManagementController : CustomBaseController
{
    private readonly IMediator _mediator;
    private readonly ICorrelationContext _correlationContext;

    public DocumentManagementController(IMediator mediator, ICorrelationContext correlationContext)
    {
        _mediator = mediator;
        _correlationContext = correlationContext;
    }

    [HttpGet("contract")]
    [HasPermission(DocumentManagementPermissions.ContractView)]
    public async Task<IActionResult> GetContract(CancellationToken ct)
    {
        var correlationId = string.IsNullOrWhiteSpace(_correlationContext.CorrelationId)
            ? HttpContext.TraceIdentifier
            : _correlationContext.CorrelationId;
        var response = await _mediator.Send(new GetDocumentManagementContractQuery(correlationId), ct);
        return CreateActionResultInstance(response);
    }
}
