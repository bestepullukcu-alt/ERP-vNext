using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Quotas.Commands;

public sealed record TryConsumeQuotaCommand(TryConsumeQuotaRequest Request) : IRequest<Response<QuotaMutationDto>>;
