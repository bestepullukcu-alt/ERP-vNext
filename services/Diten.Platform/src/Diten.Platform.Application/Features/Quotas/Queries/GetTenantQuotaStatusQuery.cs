using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Quotas.Queries;

public sealed record GetTenantQuotaStatusQuery(Guid TenantId) : IRequest<Response<IReadOnlyList<QuotaStatusDto>>>;
