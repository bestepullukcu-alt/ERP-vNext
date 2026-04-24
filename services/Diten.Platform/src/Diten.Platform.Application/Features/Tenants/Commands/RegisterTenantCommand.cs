using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commands;

public sealed record RegisterTenantCommand(
    string Name,
    string Domain,
    string? Subdomain = null,
    string? DisplayName = null,
    string? Tier = null,
    string? Region = null,
    string? Environment = null) : IRequest<Guid>;
