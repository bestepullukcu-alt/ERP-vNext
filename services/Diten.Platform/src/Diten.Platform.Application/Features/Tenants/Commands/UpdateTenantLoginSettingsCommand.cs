using Diten.Platform.Application.Features.Tenants;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commands;

public sealed record UpdateTenantLoginSettingsCommand(Guid TenantId, TenantLoginSettingsUpdateRequest Request) : IRequest<TenantLoginSettingsDto?>;
