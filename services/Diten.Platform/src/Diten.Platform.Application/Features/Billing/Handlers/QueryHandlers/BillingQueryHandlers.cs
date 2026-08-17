using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Billing.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Billing.Handlers.QueryHandlers;

public sealed class GetInvoiceQueryHandler : IRequestHandler<GetInvoiceQuery, Response<BillingInvoiceDto>>
{
    private readonly IBillingRepository _repository;

    public GetInvoiceQueryHandler(IBillingRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<BillingInvoiceDto>> Handle(GetInvoiceQuery request, CancellationToken ct)
    {
        var invoice = await _repository.GetInvoiceAsync(request.InvoiceId, ct);
        return invoice is null
            ? Response<BillingInvoiceDto>.Fail("Invoice was not found.", 404)
            : Response<BillingInvoiceDto>.Success(BillingMapper.ToDto(invoice));
    }
}

public sealed class GetInvoicesQueryHandler : IRequestHandler<GetInvoicesQuery, Response<IReadOnlyList<BillingInvoiceDto>>>
{
    private readonly IBillingRepository _repository;

    public GetInvoicesQueryHandler(IBillingRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<IReadOnlyList<BillingInvoiceDto>>> Handle(GetInvoicesQuery request, CancellationToken ct)
    {
        var invoices = await _repository.GetInvoicesAsync(ct);
        return Response<IReadOnlyList<BillingInvoiceDto>>.Success(invoices.Select(BillingMapper.ToDto).ToList());
    }
}

public sealed class GetBillingPlansQueryHandler : IRequestHandler<GetBillingPlansQuery, Response<IReadOnlyList<BillingPlanDto>>>
{
    private readonly IBillingRepository _repository;

    public GetBillingPlansQueryHandler(IBillingRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<IReadOnlyList<BillingPlanDto>>> Handle(GetBillingPlansQuery request, CancellationToken ct)
    {
        var plans = await _repository.GetPlansAsync(ct);
        return Response<IReadOnlyList<BillingPlanDto>>.Success(plans.Select(BillingMapper.ToDto).ToList());
    }
}
