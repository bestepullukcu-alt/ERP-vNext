namespace Diten.Platform.Application.Features.Tenants.Commands;

public sealed record TenantBulkDeleteRequest(IReadOnlyList<Guid> Ids);
