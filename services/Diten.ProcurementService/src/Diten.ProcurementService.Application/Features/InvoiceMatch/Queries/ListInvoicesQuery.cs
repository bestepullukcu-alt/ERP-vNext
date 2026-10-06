using Diten.ProcurementService.Domain.Entities;
using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.InvoiceMatch.Queries;

/// <summary>listInvoices (contract GET /api/invoice-match/invoices). Tenant+LE filtreli; opsiyonel supplierId + status
/// + cursor sayfalama. Fatura register list yüzeyi (exception kuyruğundan bağımsız); kardeş modül desenleriyle aynı.</summary>
public sealed record ListInvoicesQuery(
    string? SupplierId = null,
    InvoiceStatus? Status = null,
    string? Cursor = null) : IRequest<Response<InvoiceListResultDto>>;
