using Diten.TalentEcosystemService.Api.Controllers.Common;
using Diten.TalentEcosystemService.Api.Security;
using Diten.TalentEcosystemService.Application.Features.PayBenchmarking;
using Diten.TalentEcosystemService.Application.Features.PayBenchmarking.Commands;
using Diten.TalentEcosystemService.Application.Features.PayBenchmarking.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.TalentEcosystemService.Api.Controllers.Tep;

[Route("api/salary-benchmarking")]
[Authorize]
public sealed class PayBenchmarkingController : CustomBaseController
{
    private readonly IMediator _mediator;

    public PayBenchmarkingController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(PayBenchmarkingGuard.ReadPermission)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetPayBenchmarkingReadinessListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(PayBenchmarkingGuard.ReadPermission)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetPayBenchmarkingReadinessByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(PayBenchmarkingGuard.ManagePermission)]
    public async Task<IActionResult> Create([FromBody] PayBenchmarkingReadinessCreateRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreatePayBenchmarkingReadinessCommand(request), ct));

    [HttpPost("{id:guid}/evaluate")]
    [HasPermission(PayBenchmarkingGuard.EvaluatePermission)]
    public async Task<IActionResult> Evaluate(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluatePayBenchmarkingReadinessCommand(id), ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(PayBenchmarkingGuard.ManagePermission)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new DeletePayBenchmarkingReadinessCommand(id), ct));

    [HttpGet("{id:guid}/audit-metadata")]
    [HasPermission(PayBenchmarkingGuard.AuditReadPermission)]
    public async Task<IActionResult> GetAuditMetadata(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetPayBenchmarkingAuditMetadataQuery(id), ct));
}
