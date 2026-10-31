using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MediatR;
using Diten.SupplyChainService.Api.Controllers;
using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Application.Features.SupplierPerformance;
using Diten.SupplyChainService.Application.Features.SupplierPerformance.Commands;
using Diten.SupplyChainService.Application.Features.SupplierPerformance.Queries;
using Diten.SupplyChainService.Infrastructure.Features.SupplierPerformance;
namespace Diten.SupplyChainService.Api.Features.SupplierPerformance;

/// <summary>
/// SUPPLIER-PERFORMANCE v1 (frozen), DL-1 operations of MOD-0147. No Gateway route and no catalog permission yet: both ship
/// with the enabling change (pack §0.0, F-1). The context middleware runs §0.5 stages 1–3b before any action.
/// </summary>
[Authorize, Route("api/supplier-performance")]
public sealed class SupplierPerformanceController(ISender sender, SupplierPerformanceRequestContext context, ILogger<SupplierPerformanceController> logger) : CustomBaseController
{
    [HttpGet("evaluations"), SupplierPerformancePermission(SupplierPerformancePermissions.Read)]
    public async Task<IActionResult> ListEvaluations([FromQuery] string? supplierId, [FromQuery] string? status, [FromQuery] string? periodFrom,
        [FromQuery] string? periodTo, [FromQuery] string? page, [FromQuery] string? pageSize, CancellationToken ct) =>
        Answer("listSupplierEvaluations", await sender.Send(new GetSupplierEvaluationListQuery(supplierId, status, periodFrom, periodTo, page, pageSize), ct));

    [HttpPost("evaluations"), SupplierPerformancePermission(SupplierPerformancePermissions.Evaluate, ScopeNotFoundCode = SupplierPerformanceErrorCodes.UnknownSupplier)]
    public async Task<IActionResult> CreateEvaluation([FromBody] JsonElement body, CancellationToken ct) =>
        Answer("createSupplierEvaluation", await sender.Send(new CreateSupplierEvaluationCommand(body), ct));

    [HttpGet("evaluations/{evaluationId}"), SupplierPerformancePermission(SupplierPerformancePermissions.Read, ScopeNotFoundCode = SupplierPerformanceErrorCodes.UnknownEvaluation)]
    public async Task<IActionResult> GetEvaluation(string evaluationId, CancellationToken ct) =>
        Answer("getSupplierEvaluation", await sender.Send(new GetSupplierEvaluationQuery(Guid.Parse(evaluationId)), ct));

    [HttpGet("scorecards"), SupplierPerformancePermission(SupplierPerformancePermissions.Read)]
    public async Task<IActionResult> ListScorecards([FromQuery] string? supplierId, [FromQuery] string? periodFrom, [FromQuery] string? periodTo,
        [FromQuery] string? riskLevel, CancellationToken ct) =>
        Answer("listSupplierScorecards", await sender.Send(new GetSupplierScorecardListQuery(supplierId, periodFrom, periodTo, riskLevel), ct));

    [HttpGet("scorecards/{scorecardId}"), SupplierPerformancePermission(SupplierPerformancePermissions.Read, ScopeNotFoundCode = SupplierPerformanceErrorCodes.UnknownScorecard)]
    public async Task<IActionResult> GetScorecard(string scorecardId, CancellationToken ct) =>
        Answer("getSupplierScorecard", await sender.Send(new GetSupplierScorecardQuery(Guid.Parse(scorecardId)), ct));

    private IActionResult Answer<T>(string operation, Response<T> r)
    {
        logger.LogInformation("Supplier performance outcome {Operation} {Status} {ErrorCode}", operation, r.StatusCode, r.ErrorCode);
        return StatusCode(r.StatusCode, r.ErrorCode is null ? r.Data
            : SupplierPerformanceContractError.Create(r.StatusCode == 500 ? SupplierPerformanceErrorCodes.InternalError : r.ErrorCode, r.StatusCode, context.CorrelationId, r.Details));
    }
}
