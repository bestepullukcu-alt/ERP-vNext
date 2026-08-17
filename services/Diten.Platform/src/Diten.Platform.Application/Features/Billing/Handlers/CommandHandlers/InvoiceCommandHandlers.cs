using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Billing.Commands;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums.Billing;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Billing.Handlers.CommandHandlers;

public sealed class CreateInvoiceCommandHandler : IRequestHandler<CreateInvoiceCommand, Response<BillingInvoiceDto>>
{
    private readonly IBillingRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;

    public CreateInvoiceCommandHandler(IBillingRepository repository, ITenantContext tenantContext, ICurrentUserContext currentUser)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
    }

    public async Task<Response<BillingInvoiceDto>> Handle(CreateInvoiceCommand request, CancellationToken ct)
    {
        var plan = await _repository.GetPlanAsync(request.Request.BillingPlanId, ct);
        if (plan is null || plan.Status != BillingPlanStatus.Active)
        {
            return Response<BillingInvoiceDto>.Fail("Active billing plan was not found.", 404);
        }

        var invoice = BuildInvoice(
            _tenantContext.TenantId,
            _currentUser.UserId.ToString(),
            plan,
            request.Request.CustomerReference,
            request.Request.DueDate,
            request.Request.Notes,
            request.Request.Lines);

        await _repository.CreateInvoiceAsync(invoice, ct);
        return Response<BillingInvoiceDto>.Success(BillingMapper.ToDto(invoice), 201);
    }

    internal static BillingInvoice BuildInvoice(
        Guid tenantId,
        string actorUserId,
        BillingPlan plan,
        string customerReference,
        DateTimeOffset? dueDate,
        string? notes,
        IReadOnlyList<InvoiceLineRequest> lineRequests)
    {
        var currency = BillingMoney.NormalizeCurrency(plan.Currency);
        var lines = lineRequests.Select(line =>
        {
            var quantity = BillingMoney.Round(line.Quantity);
            var unitPrice = BillingMoney.Round(line.UnitPrice);
            var discount = BillingMoney.Round(line.DiscountAmount);
            var tax = BillingMoney.Round(line.TaxAmount);
            var lineTotal = BillingMoney.Round(quantity * unitPrice - discount + tax);
            return new InvoiceLine
            {
                Description = line.Description.Trim(),
                Currency = currency,
                Quantity = quantity,
                UnitPrice = unitPrice,
                DiscountAmount = discount,
                TaxAmount = tax,
                LineTotal = lineTotal
            };
        }).ToList();

        var subTotal = BillingMoney.Round(lines.Sum(x => x.Quantity * x.UnitPrice));
        var discountTotal = BillingMoney.Round(lines.Sum(x => x.DiscountAmount));
        var taxTotal = BillingMoney.Round(lines.Sum(x => x.TaxAmount));
        var grandTotal = BillingMoney.Round(subTotal + taxTotal - discountTotal);

        return new BillingInvoice
        {
            TenantId = tenantId,
            CreatedBy = actorUserId,
            BillingPlanId = plan.Id,
            BillingPlanVersion = plan.PlanVersion,
            BillingPlanNameSnapshot = plan.Name,
            BillingPlanAmountSnapshot = BillingMoney.Round(plan.Amount),
            BillingPlanCurrencySnapshot = currency,
            CustomerReference = customerReference.Trim(),
            Currency = currency,
            DueDate = dueDate,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            Lines = lines,
            SubTotal = subTotal,
            TaxTotal = taxTotal,
            DiscountTotal = discountTotal,
            GrandTotal = grandTotal,
            PaidAmount = 0,
            BalanceDue = grandTotal
        };
    }
}

public sealed class UpdateInvoiceCommandHandler : IRequestHandler<UpdateInvoiceCommand, Response<BillingInvoiceDto>>
{
    private readonly IBillingRepository _repository;
    private readonly ICurrentUserContext _currentUser;

    public UpdateInvoiceCommandHandler(IBillingRepository repository, ICurrentUserContext currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<Response<BillingInvoiceDto>> Handle(UpdateInvoiceCommand request, CancellationToken ct)
    {
        var existing = await _repository.GetInvoiceAsync(request.InvoiceId, ct);
        if (existing is null)
        {
            return Response<BillingInvoiceDto>.Fail("Invoice was not found.", 404);
        }

        if (existing.Status != InvoiceStatus.Draft)
        {
            return Response<BillingInvoiceDto>.Fail("Only draft invoices can be edited.", 409);
        }

        var plan = await _repository.GetPlanAsync(existing.BillingPlanId, ct);
        if (plan is null)
        {
            return Response<BillingInvoiceDto>.Fail("Billing plan was not found.", 404);
        }

        var updated = CreateInvoiceCommandHandler.BuildInvoice(
            existing.TenantId,
            existing.CreatedBy,
            plan,
            request.Request.CustomerReference,
            request.Request.DueDate,
            request.Request.Notes,
            request.Request.Lines);

        existing.CustomerReference = updated.CustomerReference;
        existing.DueDate = updated.DueDate;
        existing.Notes = updated.Notes;
        existing.Lines = updated.Lines;
        existing.SubTotal = updated.SubTotal;
        existing.TaxTotal = updated.TaxTotal;
        existing.DiscountTotal = updated.DiscountTotal;
        existing.GrandTotal = updated.GrandTotal;
        existing.BalanceDue = updated.GrandTotal;
        existing.UpdatedAt = DateTimeOffset.UtcNow;
        existing.UpdatedBy = _currentUser.UserId.ToString();

        var succeeded = await _repository.UpdateDraftInvoiceAsync(existing, ct);
        return succeeded
            ? Response<BillingInvoiceDto>.Success(BillingMapper.ToDto(existing))
            : Response<BillingInvoiceDto>.Fail("Only draft invoices can be edited.", 409);
    }
}

public sealed class IssueInvoiceCommandHandler : IRequestHandler<IssueInvoiceCommand, Response<BillingInvoiceDto>>
{
    private readonly IBillingRepository _repository;

    public IssueInvoiceCommandHandler(IBillingRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<BillingInvoiceDto>> Handle(IssueInvoiceCommand request, CancellationToken ct)
    {
        var result = await _repository.IssueInvoiceAsync(request.InvoiceId, request.ActorUserId, ct);
        return result.Succeeded && result.Invoice is not null
            ? Response<BillingInvoiceDto>.Success(BillingMapper.ToDto(result.Invoice))
            : Response<BillingInvoiceDto>.Fail("Invoice was not found or is not draft.", 409);
    }
}

public sealed class CancelInvoiceCommandHandler : IRequestHandler<CancelInvoiceCommand, Response<BillingInvoiceDto>>
{
    private readonly IBillingRepository _repository;

    public CancelInvoiceCommandHandler(IBillingRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<BillingInvoiceDto>> Handle(CancelInvoiceCommand request, CancellationToken ct)
    {
        var result = await _repository.CancelInvoiceAsync(request.InvoiceId, ct);
        return result.Succeeded && result.Invoice is not null
            ? Response<BillingInvoiceDto>.Success(BillingMapper.ToDto(result.Invoice))
            : Response<BillingInvoiceDto>.Fail("Invoice can be cancelled only when issued and unpaid.", 409);
    }
}
