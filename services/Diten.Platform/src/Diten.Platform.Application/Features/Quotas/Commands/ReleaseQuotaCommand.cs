using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Quotas.Commands;

public sealed record ReleaseQuotaCommand(ReleaseQuotaRequest Request) : IRequest<Response<QuotaMutationDto>>;
