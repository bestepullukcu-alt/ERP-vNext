using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Queries;

public sealed record GetTenantModuleEffectiveAccessQuery(Guid TenantId, string ModuleCode) : IRequest<Response<TenantModuleEffectiveAccessDto>>;
