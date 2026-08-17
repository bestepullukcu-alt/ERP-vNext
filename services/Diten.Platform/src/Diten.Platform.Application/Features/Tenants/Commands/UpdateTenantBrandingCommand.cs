using Diten.Platform.Application.Features.Tenants;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commands;

public sealed record UpdateTenantBrandingCommand(Guid TenantId, TenantBrandingUpdateRequest Request) : IRequest<TenantDetailDto?>;
