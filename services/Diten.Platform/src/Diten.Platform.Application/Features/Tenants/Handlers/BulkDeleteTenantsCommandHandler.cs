using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tenants.Commands;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Handlers;

public sealed class BulkDeleteTenantsCommandHandler : IRequestHandler<BulkDeleteTenantsCommand, Response<NoContent>>
{
    private readonly ITenantRegistryRepository _repository;

    public BulkDeleteTenantsCommandHandler(ITenantRegistryRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<NoContent>> Handle(BulkDeleteTenantsCommand request, CancellationToken ct)
    {
        var ids = request.Ids.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return Response<NoContent>.Fail("At least one tenant id is required.", 400);
        }

        foreach (var id in ids)
        {
            var tenant = await _repository.GetByIdAsync(id, ct);
            if (tenant is null)
            {
                return Response<NoContent>.Fail("Tenant not found.", 404);
            }

            if (tenant.Status == TenantStatus.Active)
            {
                return Response<NoContent>.Fail("Active tenants must be suspended before deletion.", 400);
            }
        }

        foreach (var id in ids)
        {
            await _repository.DeleteAsync(id, ct);
        }

        return Response<NoContent>.Success(204);
    }
}
