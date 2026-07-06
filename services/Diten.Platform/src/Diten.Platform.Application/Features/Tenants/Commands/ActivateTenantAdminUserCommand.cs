using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commands;

// FIX-TENANT-ADMIN-INVITE-ACTIVATION (Part B) — AuthService S2S callback: the invited admin (email) of tenant
// (tenantId) completed its forced first-login password change → flip the matching TenantAdminUser Invited → Active.
// Idempotent: already-Active or no matching admin is a successful no-op.
public sealed record ActivateTenantAdminUserCommand(Guid TenantId, string Email) : IRequest<Response<NoContent>>;
