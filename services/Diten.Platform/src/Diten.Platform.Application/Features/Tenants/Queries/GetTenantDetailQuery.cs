using Diten.Platform.Application.Features.Tenants;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Queries;

public sealed record GetTenantDetailQuery(Guid TenantId) : IRequest<TenantDetailDto?>;
