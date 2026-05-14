using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Quotas.Commands;

public sealed record RecalculateQuotaUsageCommand(RecalculateQuotaUsageRequest Request) : IRequest<Response<QuotaStatusDto>>;
