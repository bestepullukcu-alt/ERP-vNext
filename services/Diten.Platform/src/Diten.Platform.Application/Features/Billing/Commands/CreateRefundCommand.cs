using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Billing.Commands;

public sealed record CreateRefundCommand(Guid PaymentRecordId, CreateRefundRequest Request) : IRequest<Response<RefundRecordDto>>;
