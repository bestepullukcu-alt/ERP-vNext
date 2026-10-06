using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.InvoiceMatch.Queries;

/// <summary>getInvoice (contract GET /api/invoice-match/invoices/{invoiceId}). Cross-tenant/LE → 404 NOT_FOUND (sızıntı yok).</summary>
public sealed record GetInvoiceByIdQuery(string InvoiceId) : IRequest<Response<InvoiceDto>>;
