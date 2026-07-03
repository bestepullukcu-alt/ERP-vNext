using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Navigation;
using MediatR;

namespace Diten.Platform.Application.Features.Navigation.Queries;

/// <summary>FEAT-TENANT-NAV-PREFS — the tenant's current sidebar preferences (module + domain; empty when none set).</summary>
public sealed record GetTenantNavPreferencesQuery(Guid TenantId)
    : IRequest<Response<TenantNavPreferencesDto>>;
