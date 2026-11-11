using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.RecordCodes;
using Diten.Platform.Application.Features.RecordCodes.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Internal;

/// <summary>
/// MOD-0040-FU02 (RCS-001 §5.12) — the shared record-code service. Service to service, on behalf of a user; not routed by
/// the Gateway. Anonymous to the framework: the gate is <see cref="ITrustedRecordCodeRequestExecutor"/>.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/internal/record-codes")]
public sealed class InternalRecordCodesController : CustomBaseController
{
    private readonly IMediator _mediator;
    private readonly ITrustedRecordCodeRequestExecutor _executor;

    public InternalRecordCodesController(IMediator mediator, ITrustedRecordCodeRequestExecutor executor)
    {
        _mediator = mediator;
        _executor = executor;
    }

    [HttpPost("allocate")]
    public Task<IActionResult> Allocate(CancellationToken cancellationToken) =>
        _executor.ExecuteAsync(
            HttpContext,
            cancellationToken,
            async (caller, token) => CreateActionResultInstance(
                await _mediator.Send(
                    new AllocateRecordCodeCommand(
                        caller.TenantId,
                        caller.SubjectId,
                        caller.CallerService,
                        caller.AllowedRecordTypes,
                        caller.Request,
                        caller.RequestErrors ?? [],
                        caller.HasRequiredPermission,
                        caller.CorrelationId),
                    token)),
            Failure);

    private IActionResult Failure(int statusCode, string code) =>
        CreateActionResultInstance(
            Response<RecordCodeAllocationResult>.Fail(code, statusCode, code, HttpContext.TraceIdentifier));
}
