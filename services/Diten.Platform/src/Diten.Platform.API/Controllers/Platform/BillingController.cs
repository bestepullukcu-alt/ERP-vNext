using System.Security.Claims;
using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Billing;
using Diten.Platform.Application.Features.Billing.Commands;
using Diten.Platform.Application.Features.Billing.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Platform;

[ApiController]
[Route("api/platform/billing")]
[Authorize(Policy = "PlatformActor")]
public sealed class BillingController : CustomBaseController
{
    private readonly IMediator _mediator;

    public BillingController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("plans")]
    [HasPermission("Billing.Plans.Create")]
    public async Task<IActionResult> CreatePlan([FromBody] CreateBillingPlanRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new CreateBillingPlanCommand(request), ct);
        return CreateActionResult(response);
    }

    [HttpGet("plans")]
    [HasPermission("Billing.Plans.Read")]
    public async Task<IActionResult> GetPlans(CancellationToken ct)
    {
        var response = await _mediator.Send(new GetBillingPlansQuery(), ct);
        return CreateActionResult(response);
    }

    [HttpPost("plans/{id:guid}/activate")]
    [HasPermission("Billing.Plans.Update")]
    public async Task<IActionResult> ActivatePlan(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new ActivateBillingPlanCommand(id), ct);
        return CreateActionResult(response);
    }

    [HttpPost("plans/{id:guid}/revisions")]
    [HasPermission("Billing.Plans.Update")]
    public async Task<IActionResult> RevisePlan(Guid id, [FromBody] ReviseBillingPlanRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new ReviseBillingPlanCommand(id, request), ct);
        return CreateActionResult(response);
    }

    [HttpGet("invoices")]
    [HasPermission("Billing.Invoices.Read")]
    public async Task<IActionResult> GetInvoices(CancellationToken ct)
    {
        var response = await _mediator.Send(new GetInvoicesQuery(), ct);
        return CreateActionResult(response);
    }

    [HttpGet("invoices/{id:guid}")]
    [HasPermission("Billing.Invoices.Read")]
    public async Task<IActionResult> GetInvoice(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetInvoiceQuery(id), ct);
        return CreateActionResult(response);
    }

    [HttpPost("invoices")]
    [HasPermission("Billing.Invoices.Create")]
    public async Task<IActionResult> CreateInvoice([FromBody] CreateInvoiceRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new CreateInvoiceCommand(request), ct);
        return CreateActionResult(response);
    }

    [HttpPut("invoices/{id:guid}")]
    [HasPermission("Billing.Invoices.Update")]
    public async Task<IActionResult> UpdateInvoice(Guid id, [FromBody] UpdateInvoiceRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new UpdateInvoiceCommand(id, request), ct);
        return CreateActionResult(response);
    }

    [HttpPost("invoices/{id:guid}/issue")]
    [HasPermission("Billing.Invoices.Issue")]
    public async Task<IActionResult> IssueInvoice(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new IssueInvoiceCommand(id, GetActorUserId()), ct);
        return CreateActionResult(response);
    }

    [HttpPost("invoices/{id:guid}/cancel")]
    [HasPermission("Billing.Invoices.Cancel")]
    public async Task<IActionResult> CancelInvoice(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new CancelInvoiceCommand(id), ct);
        return CreateActionResult(response);
    }

    [HttpPost("invoices/{id:guid}/payments")]
    [HasPermission("Billing.Payments.Create")]
    public async Task<IActionResult> CreatePayment(Guid id, [FromBody] CreatePaymentRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new CreatePaymentCommand(id, request), ct);
        return CreateActionResult(response);
    }

    [HttpPost("payments/{paymentRecordId:guid}/refunds")]
    [HasPermission("Billing.Refunds.Create")]
    public async Task<IActionResult> CreateRefund(Guid paymentRecordId, [FromBody] CreateRefundRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new CreateRefundCommand(paymentRecordId, request), ct);
        return CreateActionResult(response);
    }

    private string GetActorUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")
        ?? "system";

    private IActionResult CreateActionResult<T>(Response<T> response)
    {
        var payload = new
        {
            data = response.Data,
            statusCode = response.StatusCode,
            isSuccessful = response.IsSuccessful,
            succeeded = response.IsSuccessful,
            errors = response.Errors,
            message = response.IsSuccessful ? "OK" : string.Join(" ", response.Errors)
        };

        return response.StatusCode switch
        {
            200 => Ok(payload),
            201 => StatusCode(StatusCodes.Status201Created, payload),
            204 => NoContent(),
            400 => BadRequest(payload),
            403 => StatusCode(StatusCodes.Status403Forbidden, payload),
            404 => NotFound(payload),
            409 => Conflict(payload),
            _ => StatusCode(response.StatusCode, payload)
        };
    }
}
