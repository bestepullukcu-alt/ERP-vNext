using Diten.ProcurementService.Application.Features.Clause;
using Diten.ProcurementService.Application.Features.Clause.Commands;
using Diten.ProcurementService.Application.Features.Clause.Queries;
using Diten.ProcurementService.Application.Features.Contract;
using Diten.ProcurementService.Application.Features.Contract.Commands;
using Diten.ProcurementService.Application.Features.Contract.Queries;
using Diten.ProcurementService.Domain.Entities;
using Diten.ProcurementService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.ProcurementService.Api.Controllers;

/// <summary>
/// Contracting &amp; Clause Library API (MOD-0144). CONTRACTING owned contract (contracting.openapi.yaml, server
/// /api/contracts) yüzeyini birebir uygular: POST / (createContract, Idempotency-Key), GET / (listContracts;
/// supplierId + status + cursor), GET /{contractId} (getContract), POST /{contractId}/activate (activateContract,
/// Idempotency-Key), GET /clauses (listClauseLibrary; category + cursor), POST /clauses (createClause,
/// Idempotency-Key). Delete/bulk-delete pack §14 yetki yüzeyi (ASSUMPTION-0144-01 additive). Tenant + LegalEntity
/// server-resolved (TenantResolutionMiddleware); payload'da YOK. Yanıtlar Response&lt;T&gt; zarfı içinde contract
/// alanlarını korur. Her aksiyon [HasPermission("procurement.contracts.&lt;action&gt;" / "procurement.clauses.&lt;action&gt;")]
/// ile korunur (pack §14, UAS-001). Supplier (MOD-0140) + award/rfx (MOD-0145) CONSUME edilir (fail-closed → 404
/// UNKNOWN_REFERENCE); doküman BINARY saklanmaz (yalnız evidenceRef → MOD-0029/0031). Approval MOD-0023
/// (workflowInstanceId activate'te).
/// </summary>
[Authorize]
[ApiController]
[Route("api/contracts")]
public sealed class ContractsController : CustomBaseController
{
    private const string IdempotencyKeyHeader = "Idempotency-Key";

    private readonly IMediator _mediator;

    public ContractsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // ── Clause library (literal segmentler — {contractId} template'inden ÖNCE tanımlanır) ─────────

    /// <summary>listClauseLibrary — contract GET /api/contracts/clauses. category + cursor filtreli.</summary>
    [HttpGet("clauses")]
    [HasPermission("procurement.clauses.read")]
    public async Task<IActionResult> ListClauses(
        [FromQuery] string? category,
        [FromQuery] string? cursor,
        CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new ListClauseLibraryQuery(category, cursor), cancellationToken);
        return CreateActionResultInstance(response);
    }

    /// <summary>createClause — contract POST /api/contracts/clauses. Idempotency-Key ile idempotent.</summary>
    [HttpPost("clauses")]
    [HasPermission("procurement.clauses.create")]
    public async Task<IActionResult> CreateClause([FromBody] ClauseUpsertBody body, CancellationToken cancellationToken)
    {
        var command = new CreateClauseCommand(
            body.Category,
            body.Title,
            body.Body,
            ReadHeader(IdempotencyKeyHeader));

        var response = await _mediator.Send(command, cancellationToken);
        return CreateActionResultInstance(response);
    }

    // ── Contract ───────────────────────────────────────────────────────────────────────────────

    /// <summary>createContract — contract POST /api/contracts. Idempotency-Key ile idempotent.</summary>
    [HttpPost]
    [HasPermission("procurement.contracts.create")]
    public async Task<IActionResult> Create([FromBody] ContractUpsertBody body, CancellationToken cancellationToken)
    {
        var command = new CreateContractCommand(
            body.SupplierId,
            body.RfxId,
            body.Title,
            body.EffectiveFrom,
            body.EffectiveTo,
            body.Currency,
            body.Clauses,
            body.EvidenceRefs,
            ReadHeader(IdempotencyKeyHeader));

        var response = await _mediator.Send(command, cancellationToken);
        return CreateActionResultInstance(response);
    }

    /// <summary>listContracts — contract GET /api/contracts. supplierId + status + cursor filtreli.</summary>
    [HttpGet]
    [HasPermission("procurement.contracts.read")]
    public async Task<IActionResult> List(
        [FromQuery] string? supplierId,
        [FromQuery] ContractStatus? status,
        [FromQuery] string? cursor,
        CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new ListContractsQuery(supplierId, status, cursor), cancellationToken);
        return CreateActionResultInstance(response);
    }

    /// <summary>getContract — contract GET /api/contracts/{contractId}. Cross-tenant/LE → 404 NOT_FOUND.</summary>
    [HttpGet("{contractId}")]
    [HasPermission("procurement.contracts.read")]
    public async Task<IActionResult> Get(string contractId, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new GetContractByIdQuery(contractId), cancellationToken);
        return CreateActionResultInstance(response);
    }

    /// <summary>activateContract — contract POST /api/contracts/{contractId}/activate. Idempotency-Key ile idempotent.</summary>
    [HttpPost("{contractId}/activate")]
    [HasPermission("procurement.contracts.activate")]
    public async Task<IActionResult> Activate(string contractId, CancellationToken cancellationToken)
    {
        var command = new ActivateContractCommand(contractId, ReadHeader(IdempotencyKeyHeader));
        var response = await _mediator.Send(command, cancellationToken);
        return CreateActionResultInstance(response);
    }

    /// <summary>updateContract — pack §14 procurement.contracts.update (ASSUMPTION-0144-01 additive; yalnız Draft; aksi 409).</summary>
    [HttpPatch("{contractId}")]
    [HasPermission("procurement.contracts.update")]
    public async Task<IActionResult> Update(string contractId, [FromBody] ContractUpsertBody body, CancellationToken cancellationToken)
    {
        var command = new UpdateContractCommand(
            contractId,
            body.RfxId,
            body.Title,
            body.EffectiveFrom,
            body.EffectiveTo,
            body.Currency,
            body.Clauses,
            body.EvidenceRefs);

        var response = await _mediator.Send(command, cancellationToken);
        return CreateActionResultInstance(response);
    }

    /// <summary>terminateContract — pack §14 procurement.contracts.update altında (ASSUMPTION-0144-02 additive; Active→Terminated; aksi 409).</summary>
    [HttpPost("{contractId}/terminate")]
    [HasPermission("procurement.contracts.update")]
    public async Task<IActionResult> Terminate(string contractId, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new TerminateContractCommand(contractId), cancellationToken);
        return CreateActionResultInstance(response);
    }

    /// <summary>Soft delete — pack §14 procurement.contracts.delete (yalnız Draft; aktive edilmiş → 409). Hard delete YOK.</summary>
    [HttpDelete("{contractId}")]
    [HasPermission("procurement.contracts.delete")]
    public async Task<IActionResult> Delete(string contractId, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new DeleteContractCommand(contractId), cancellationToken);
        return CreateActionResultInstance(response);
    }

    /// <summary>Toplu soft delete — pack §14 procurement.contracts.bulk-delete (yalnız Draft).</summary>
    [HttpDelete("bulk")]
    [HasPermission("procurement.contracts.bulk-delete")]
    public async Task<IActionResult> BulkDelete([FromBody] List<string> contractIds, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new BulkDeleteContractCommand(contractIds ?? new List<string>()), cancellationToken);
        return CreateActionResultInstance(response);
    }

    private string? ReadHeader(string name)
        => Request.Headers.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.ToString()
            : null;
}
