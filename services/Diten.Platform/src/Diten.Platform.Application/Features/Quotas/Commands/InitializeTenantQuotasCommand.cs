using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Quotas.Commands;

public sealed record InitializeTenantQuotasCommand(Guid TenantId, InitializeTenantQuotasRequest Request) : IRequest<Response<IReadOnlyList<QuotaStatusDto>>>;
