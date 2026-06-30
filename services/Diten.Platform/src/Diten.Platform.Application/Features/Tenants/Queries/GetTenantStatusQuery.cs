using Diten.Platform.Application.Features.Tenants;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Queries;

/// <summary>
/// FIX-4 — per-request tenant liveness lookup for the Web shell session guard. Always resolves to a
/// non-null projection (a missing tenant is Exists=false) so the caller can fail-open on transport errors
/// yet sign out on a definitive missing/inactive answer.
/// </summary>
public sealed record GetTenantStatusQuery(Guid TenantId) : IRequest<TenantStatusDto>;
