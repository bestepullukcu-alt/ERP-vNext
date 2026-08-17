using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Billing.Commands;

public sealed record IssueInvoiceCommand(Guid InvoiceId, string ActorUserId) : IRequest<Response<BillingInvoiceDto>>;
