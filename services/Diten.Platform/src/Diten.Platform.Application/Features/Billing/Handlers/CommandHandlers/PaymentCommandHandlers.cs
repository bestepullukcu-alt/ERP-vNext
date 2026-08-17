using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Billing.Commands;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Billing.Handlers.CommandHandlers;

public sealed class CreatePaymentCommandHandler : IRequestHandler<CreatePaymentCommand, Response<PaymentRecordDto>>
{
    private readonly IBillingRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;

    public CreatePaymentCommandHandler(IBillingRepository repository, ITenantContext tenantContext, ICurrentUserContext currentUser)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
    }

    public async Task<Response<PaymentRecordDto>> Handle(CreatePaymentCommand request, CancellationToken ct)
    {
        var idempotencyKey = request.Request.IdempotencyKey.Trim();
        var existing = await _repository.GetPaymentByIdempotencyKeyAsync(request.InvoiceId, idempotencyKey, ct);
        if (existing is not null)
        {
            return Response<PaymentRecordDto>.Success(BillingMapper.ToDto(existing));
        }

        var payment = new PaymentRecord
        {
            TenantId = _tenantContext.TenantId,
            CreatedBy = _currentUser.UserId.ToString(),
            InvoiceId = request.InvoiceId,
            IdempotencyKey = idempotencyKey,
            Currency = BillingMoney.NormalizeCurrency(request.Request.Currency),
            AppliedAmount = BillingMoney.Round(request.Request.AppliedAmount),
            Reference = string.IsNullOrWhiteSpace(request.Request.Reference) ? null : request.Request.Reference.Trim()
        };

        var result = await _repository.ApplyPaymentAsync(request.InvoiceId, payment, ct);
        return result.Succeeded && result.Payment is not null
            ? Response<PaymentRecordDto>.Success(BillingMapper.ToDto(result.Payment), 201)
            : Response<PaymentRecordDto>.Fail("Payment could not be applied. Check invoice status, currency, balance, and idempotency key.", 409);
    }
}

public sealed class CreateRefundCommandHandler : IRequestHandler<CreateRefundCommand, Response<RefundRecordDto>>
{
    private readonly IBillingRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;

    public CreateRefundCommandHandler(IBillingRepository repository, ITenantContext tenantContext, ICurrentUserContext currentUser)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
    }

    public async Task<Response<RefundRecordDto>> Handle(CreateRefundCommand request, CancellationToken ct)
    {
        var refund = new RefundRecord
        {
            TenantId = _tenantContext.TenantId,
            CreatedBy = _currentUser.UserId.ToString(),
            InvoiceId = Guid.Empty,
            PaymentRecordId = request.PaymentRecordId,
            RefundAmount = BillingMoney.Round(request.Request.RefundAmount),
            Currency = BillingMoney.NormalizeCurrency(request.Request.Currency),
            Reason = request.Request.Reason.Trim()
        };

        var result = await _repository.CreateRefundAsync(request.PaymentRecordId, refund, ct);
        return result.Succeeded && result.Refund is not null
            ? Response<RefundRecordDto>.Success(BillingMapper.ToDto(result.Refund), 201)
            : Response<RefundRecordDto>.Fail("Refund could not be created. Check payment, currency, and refundable amount.", 409);
    }
}
