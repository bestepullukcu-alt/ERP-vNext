using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tenants.Commands;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Handlers;

public sealed class DeleteTenantCommandHandler : IRequestHandler<DeleteTenantCommand, Response<NoContent>>
{
    private readonly ITenantRegistryRepository _repository;

    public DeleteTenantCommandHandler(ITenantRegistryRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<NoContent>> Handle(DeleteTenantCommand request, CancellationToken ct)
    {
        var tenant = await _repository.GetByIdAsync(request.TenantId, ct);
        if (tenant is null)
        {
            return Response<NoContent>.Fail("Tenant not found.", 404);
        }

        if (tenant.Status == TenantStatus.Active)
        {
            return Response<NoContent>.Fail("Active tenants must be suspended before deletion.", 400);
        }

        await _repository.DeleteAsync(tenant.Id, ct);
        return Response<NoContent>.Success(204);
    }
}
