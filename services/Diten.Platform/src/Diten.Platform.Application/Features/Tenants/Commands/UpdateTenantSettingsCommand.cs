using Diten.Platform.Application.Features.Tenants;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commands;

public sealed record UpdateTenantSettingsCommand(Guid TenantId, TenantSettingsUpdateRequest Request) : IRequest<TenantSettingsDto?>;
