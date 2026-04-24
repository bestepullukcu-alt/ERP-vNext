using Diten.Platform.Application.Features.Tenants;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commands;

public sealed record ReactivateTenantCommand(Guid TenantId, string? Reason = null) : IRequest<TenantLifecycleResultDto?>;
