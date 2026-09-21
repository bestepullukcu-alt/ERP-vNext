using Diten.ProcurementService.Application.Features.Supplier;
using Diten.ProcurementService.Application.Features.Supplier.Commands;
using Diten.ProcurementService.Application.Features.Supplier.Queries;
using Diten.ProcurementService.Domain.Entities;
using Diten.ProcurementService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.ProcurementService.Api.Controllers;

/// <summary>
/// Supplier master + onboarding API (MOD-0140). SUPPLIER contract (supplier.openapi.yaml) yüzeyini birebir uygular.
/// Tenant + LegalEntity server-resolved (TenantResolutionMiddleware); payload'da YOK. Yanıtlar Response&lt;T&gt; zarfı
/// (house style / golden reference) içinde contract alanlarını (items/nextCursor/contractVersion, supplierId/known/status)
/// korur. Her aksiyon [HasPermission("procurement.suppliers.&lt;action&gt;")] ile korunur (pack §14, UAS-001).
/// </summary>
[Authorize]
[ApiController]
[Route("api/suppliers")]
public sealed class SuppliersController : CustomBaseController
{
    private const string IdempotencyKeyHeader = "Idempotency-Key";
    private const string IfMatchHeader = "If-Match";

    private readonly IMediator _mediator;

    public SuppliersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>listSuppliers — contract GET /. Tenant+LE filtreli; opsiyonel status + cursor.</summary>
    [HttpGet]
    [HasPermission("procurement.suppliers.read")]
    public async Task<IActionResult> GetAll(
        [FromQuery] SupplierStatus? status,
        [FromQuery] string? cursor,
        CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new GetSupplierListQuery(status, cursor), cancellationToken);
        return CreateActionResultInstance(response);
    }

    /// <summary>getSupplier — contract GET /{supplierId}.</summary>
    [HttpGet("{supplierId}")]
    [HasPermission("procurement.suppliers.read")]
    public async Task<IActionResult> GetById(string supplierId, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new GetSupplierByIdQuery(supplierId), cancellationToken);
        return CreateActionResultInstance(response);
    }

    /// <summary>createSupplier — contract POST /. Idempotency-Key ile idempotent.</summary>
    [HttpPost]
    [HasPermission("procurement.suppliers.create")]
    public async Task<IActionResult> Create([FromBody] SupplierUpsertRequest body, CancellationToken cancellationToken)
    {
        var command = new CreateSupplierCommand(
            body.Name,
            body.Country,
            body.TaxId,
            body.Contacts,
            body.SourceSystem,
            body.ExternalRef,
            ReadHeader(IdempotencyKeyHeader));

        var response = await _mediator.Send(command, cancellationToken);
        return CreateActionResultInstance(response);
    }

    /// <summary>updateSupplier — contract PATCH /{supplierId}. If-Match rowVersion → stale 409.</summary>
    [HttpPatch("{supplierId}")]
    [HasPermission("procurement.suppliers.update")]
    public async Task<IActionResult> Update(string supplierId, [FromBody] SupplierUpsertRequest body, CancellationToken cancellationToken)
    {
        var command = new UpdateSupplierCommand
        {
            SupplierId = supplierId,
            Name = body.Name,
            Country = body.Country,
            TaxId = body.TaxId,
            Contacts = body.Contacts,
            SourceSystem = body.SourceSystem,
            ExternalRef = body.ExternalRef,
            ExpectedVersion = ParseIfMatch(ReadHeader(IfMatchHeader))
        };

        var response = await _mediator.Send(command, cancellationToken);
        return CreateActionResultInstance(response);
    }

    /// <summary>Soft delete — pack §14 procurement.suppliers.delete (contract dışı; pack yetki yüzeyi).</summary>
    [HttpDelete("{supplierId}")]
    [HasPermission("procurement.suppliers.delete")]
    public async Task<IActionResult> Delete(string supplierId, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new DeleteSupplierCommand(supplierId), cancellationToken);
        return CreateActionResultInstance(response);
    }

    /// <summary>Toplu soft delete — pack §14 procurement.suppliers.bulk-delete.</summary>
    [HttpDelete("bulk")]
    [HasPermission("procurement.suppliers.bulk-delete")]
    public async Task<IActionResult> BulkDelete([FromBody] List<string> supplierIds, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new BulkDeleteSupplierCommand(supplierIds ?? new List<string>()), cancellationToken);
        return CreateActionResultInstance(response);
    }

    /// <summary>validateSuppliers — contract POST /validate. Bilinmeyen id → known:false (fail-closed).</summary>
    [HttpPost("validate")]
    [HasPermission("procurement.suppliers.read")]
    public async Task<IActionResult> Validate([FromBody] ValidateSuppliersRequest body, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new ValidateSuppliersQuery(body.SupplierIds ?? new List<string>()), cancellationToken);
        return CreateActionResultInstance(response);
    }

    /// <summary>getOnboardingCase — contract GET /{supplierId}/onboarding.</summary>
    [HttpGet("{supplierId}/onboarding")]
    [HasPermission("procurement.suppliers.read")]
    public async Task<IActionResult> GetOnboarding(string supplierId, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new GetOnboardingCaseQuery(supplierId), cancellationToken);
        return CreateActionResultInstance(response);
    }

    /// <summary>submitOnboardingCase — contract POST /{supplierId}/onboarding. Idempotency-Key ile idempotent.</summary>
    [HttpPost("{supplierId}/onboarding")]
    [HasPermission("procurement.suppliers.onboard")]
    public async Task<IActionResult> SubmitOnboarding(string supplierId, [FromBody] OnboardingSubmitRequest body, CancellationToken cancellationToken)
    {
        var command = new SubmitOnboardingCaseCommand
        {
            SupplierId = supplierId,
            Kyc = body.Kyc,
            Documents = body.Documents,
            SubmitForApproval = body.SubmitForApproval,
            IdempotencyKey = ReadHeader(IdempotencyKeyHeader)
        };

        var response = await _mediator.Send(command, cancellationToken);
        return CreateActionResultInstance(response);
    }

    private string? ReadHeader(string name)
        => Request.Headers.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.ToString()
            : null;

    /// <summary>If-Match rowVersion parse; sayı değilse concurrency zorlanmaz (null).</summary>
    private static int? ParseIfMatch(string? ifMatch)
        => int.TryParse(ifMatch?.Trim().Trim('"'), out var version) ? version : null;
}
