using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Quotas.Queries;

public sealed record GetTenantQuotaStatusByKeyQuery(Guid TenantId, string QuotaKey) : IRequest<Response<QuotaStatusDto>>;
