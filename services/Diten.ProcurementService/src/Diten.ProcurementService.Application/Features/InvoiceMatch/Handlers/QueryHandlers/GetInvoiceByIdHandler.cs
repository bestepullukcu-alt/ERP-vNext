using Diten.ProcurementService.Application.Features.InvoiceMatch.Queries;
using Diten.ProcurementService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.InvoiceMatch.Handlers.QueryHandlers;

/// <summary>getInvoice (contract GET /api/invoice-match/invoices/{invoiceId}). Cross-tenant/LE → repository null →
/// 404 NOT_FOUND (sızıntı yok).</summary>
public sealed class GetInvoiceByIdHandler : IRequestHandler<GetInvoiceByIdQuery, Response<InvoiceDto>>
{
    private readonly IInvoiceMatchRepository _repository;

    public GetInvoiceByIdHandler(IInvoiceMatchRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<InvoiceDto>> Handle(GetInvoiceByIdQuery request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByInvoiceIdAsync(request.InvoiceId, cancellationToken);
        if (entity is null)
        {
            return Response<InvoiceDto>.Fail("NOT_FOUND", 404);
        }

        return Response<InvoiceDto>.Success(InvoiceMatchMapping.ToDto(entity));
    }
}
