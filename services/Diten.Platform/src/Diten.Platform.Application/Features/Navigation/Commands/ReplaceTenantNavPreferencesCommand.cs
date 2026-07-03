using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Navigation;
using MediatR;

namespace Diten.Platform.Application.Features.Navigation.Commands;

/// <summary>
/// FEAT-TENANT-NAV-PREFS / -DOMAINS — replaces the tenant's ENTIRE sidebar preference set (per-module + per-domain).
/// Only entitled modules are persisted; non-entitled / unknown module codes are ignored. The UI submits the full
/// set at once.
/// </summary>
public sealed record ReplaceTenantNavPreferencesCommand(
    Guid TenantId,
    IReadOnlyList<TenantNavPreferenceDto> Modules,
    IReadOnlyList<TenantNavDomainPreferenceDto> Domains)
    : IRequest<Response<NoContent>>;
