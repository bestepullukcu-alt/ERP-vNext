using Diten.ProcurementService.Application.Features.InvoiceMatch.Queries;
using Diten.ProcurementService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;
using InvoiceEntity = Diten.ProcurementService.Domain.Entities.Invoice;

namespace Diten.ProcurementService.Application.Features.InvoiceMatch.Handlers.QueryHandlers;

/// <summary>listInvoices (contract GET /api/invoice-match/invoices). Repository Tenant + LegalEntity + IsDeleted=false
/// ile filtreler (sızıntı yok); opsiyonel supplierId + status; cursor sayfalama (InvoiceId sıralı). Kardeş
/// GetPurchaseOrderListHandler deseniyle aynı; exception kuyruğundan bağımsız fatura register yüzeyi.</summary>
public sealed class ListInvoicesHandler : IRequestHandler<ListInvoicesQuery, Response<InvoiceListResultDto>>
{
    private const int PageSize = 50;

    private readonly IInvoiceMatchRepository _repository;

    public ListInvoicesHandler(IInvoiceMatchRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<InvoiceListResultDto>> Handle(ListInvoicesQuery request, CancellationToken cancellationToken)
    {
        // Repository, Tenant + LegalEntity + IsDeleted=false ile filtreler (cross-LE/tenant sızıntısı yok).
        var entities = await _repository.GetAllAsync(request.SupplierId, request.Status, cancellationToken);

        // Cursor = son dönen InvoiceId (opaque). Kararlı sıralama InvoiceId.
        var ordered = entities
            .OrderBy(x => x.InvoiceId, StringComparer.Ordinal)
            .ToList();

        IEnumerable<InvoiceEntity> page = ordered;
        if (!string.IsNullOrWhiteSpace(request.Cursor))
        {
            page = ordered.Where(x => string.CompareOrdinal(x.InvoiceId, request.Cursor) > 0);
        }

        var pageList = page.Take(PageSize + 1).ToList();
        var hasMore = pageList.Count > PageSize;
        var items = pageList.Take(PageSize).Select(InvoiceMatchMapping.ToDto).ToList();
        var nextCursor = hasMore ? pageList[PageSize - 1].InvoiceId : null;

        var result = new InvoiceListResultDto(items, nextCursor, InvoiceMatchContract.Version);
        return Response<InvoiceListResultDto>.Success(result);
    }
}
