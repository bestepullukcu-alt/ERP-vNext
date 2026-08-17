using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Billing.Commands;

public sealed record CreateInvoiceCommand(CreateInvoiceRequest Request) : IRequest<Response<BillingInvoiceDto>>;
