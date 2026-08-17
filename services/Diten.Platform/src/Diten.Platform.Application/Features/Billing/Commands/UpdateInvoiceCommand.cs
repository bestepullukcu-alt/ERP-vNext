using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Billing.Commands;

public sealed record UpdateInvoiceCommand(Guid InvoiceId, UpdateInvoiceRequest Request) : IRequest<Response<BillingInvoiceDto>>;
