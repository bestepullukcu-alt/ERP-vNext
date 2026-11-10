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
/// korur. Her aksiyon [HasPermission("procurement.suppliers.&lt;action&gt;")] ile korunur (pack §14, UAS-001); the three
/// eligibility endpoints take <c>eligibility.read</c> OR <c>read</c> ([HasAnyPermission], MOD-0140 DL-3).
/// </summary>
[Authorize]
[ApiController]
[Route("api/suppliers")]
public sealed class SuppliersController : CustomBaseController
{
    private const string IdempotencyKeyHeader = "Idempotency-Key";
    private const string IfMatchHeader = "If-Match";

    private const string MasterUpdatePolicy = "Permission:procurement.suppliers.master.update";

    private readonly IMediator _mediator;
    private readonly IAuthorizationService _authorization;

    public SuppliersController(IMediator mediator, IAuthorizationService authorization)
    {
        _mediator = mediator;
        _authorization = authorization;
    }

    /// <summary>
    /// listEligibleSuppliers — 1.3.0 GET /eligible (§3.1, F-2): the caller company's eligible suppliers, by code. A literal
    /// segment, so it wins over <c>{supplierId}</c>. <c>eligibility.read</c> OR <c>read</c> (§14).
    /// </summary>
    [HttpGet("eligible")]
    [HasAnyPermission("procurement.suppliers.eligibility.read", "procurement.suppliers.read")]
    public async Task<IActionResult> GetEligible([FromQuery] string? cursor, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new ListEligibleSuppliersQuery(cursor), cancellationToken);
        return CreateActionResultInstance(response);
    }

    /// <summary>getSupplierEligibility — 1.3.0 GET /{supplierId}/eligibility (§3.1): code or supplierMasterId uuid.</summary>
    [HttpGet("{supplierId}/eligibility")]
    [HasAnyPermission("procurement.suppliers.eligibility.read", "procurement.suppliers.read")]
    public async Task<IActionResult> GetEligibility(string supplierId, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new GetSupplierEligibilityQuery(supplierId), cancellationToken);
        return CreateActionResultInstance(response);
    }

    /// <summary>getSupplierRelationship — 1.4.0 GET /{supplierId}/relationship (K-1): the caller company's relationship.</summary>
    [HttpGet("{supplierId}/relationship")]
    [HasPermission("procurement.suppliers.read")]
    public async Task<IActionResult> GetRelationship(string supplierId, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new GetSupplierRelationshipQuery(supplierId), cancellationToken);
        return CreateActionResultInstance(response);
    }

    /// <summary>linkSupplierToCompany — 1.4.0 POST /{supplierId}/relationship (K-1, K-6). Idempotency-Key; exists → 409.</summary>
    [HttpPost("{supplierId}/relationship")]
    [HasPermission("procurement.suppliers.create")]
    public async Task<IActionResult> LinkRelationship(string supplierId, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new LinkSupplierToCompanyCommand(supplierId, ReadHeader(IdempotencyKeyHeader)), cancellationToken);
        return CreateActionResultInstance(response);
    }

    /// <summary>changeRelationshipStatus — 1.4.0 PUT /{supplierId}/relationship/status (K-1). If-Match.</summary>
    [HttpPut("{supplierId}/relationship/status")]
    [HasPermission("procurement.suppliers.update")]
    public async Task<IActionResult> ChangeRelationshipStatus(string supplierId, [FromBody] RelationshipStatusChangeRequest body, CancellationToken cancellationToken)
    {
        var command = new ChangeRelationshipStatusCommand(supplierId, body.Status, ParseIfMatch(ReadHeader(IfMatchHeader)));
        var response = await _mediator.Send(command, cancellationToken);
        return CreateActionResultInstance(response);
    }

    /// <summary>setRelationshipPaymentTerms — 1.4.0 PUT /{supplierId}/relationship/payment-terms (K-1, K-8). If-Match.</summary>
    [HttpPut("{supplierId}/relationship/payment-terms")]
    [HasPermission("procurement.suppliers.update")]
    public async Task<IActionResult> SetRelationshipPaymentTerms(string supplierId, [FromBody] RelationshipPaymentTermsRequest body, CancellationToken cancellationToken)
    {
        var command = new SetRelationshipPaymentTermsCommand(supplierId, body.PaymentTermsCode, ParseIfMatch(ReadHeader(IfMatchHeader)));
        var response = await _mediator.Send(command, cancellationToken);
        return CreateActionResultInstance(response);
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
        // K-1 (§14): the common fields need procurement.suppliers.master.update as well — checked with the SAME policy pipeline
        // as [HasPermission]. With it: UpdateSupplierMaster; without it: UpdateSupplier, which refuses a common-field change
        // with 403 MASTER_UPDATE_FORBIDDEN under TenantShared and writes nothing.
        var expectedVersion = ParseIfMatch(ReadHeader(IfMatchHeader));
        if ((await _authorization.AuthorizeAsync(User, MasterUpdatePolicy)).Succeeded)
        {
            var master = new UpdateSupplierMasterCommand
            {
                SupplierId = supplierId,
                Name = body.Name,
                Country = body.Country,
                TaxId = body.TaxId,
                Contacts = body.Contacts,
                SourceSystem = body.SourceSystem,
                ExternalRef = body.ExternalRef,
                ExpectedVersion = expectedVersion
            };
            return CreateActionResultInstance(await _mediator.Send(master, cancellationToken));
        }

        var command = new UpdateSupplierCommand
        {
            SupplierId = supplierId,
            Name = body.Name,
            Country = body.Country,
            TaxId = body.TaxId,
            Contacts = body.Contacts,
            SourceSystem = body.SourceSystem,
            ExternalRef = body.ExternalRef,
            ExpectedVersion = expectedVersion
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
    [HasAnyPermission("procurement.suppliers.read", "procurement.suppliers.eligibility.read")]
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
