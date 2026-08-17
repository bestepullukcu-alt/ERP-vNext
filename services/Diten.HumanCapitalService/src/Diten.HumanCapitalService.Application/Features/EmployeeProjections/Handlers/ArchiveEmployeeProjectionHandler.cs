using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.EmployeeProjections.Commands;
using Diten.HumanCapitalService.Domain.Enums;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.EmployeeProjections.Handlers;

public sealed class ArchiveEmployeeProjectionHandler : IRequestHandler<ArchiveEmployeeProjectionCommand, Response<NoContent>>
{
    private readonly IEmployeeProjectionRepository _repository;
    private readonly ITenantContext _tenantContext;

    public ArchiveEmployeeProjectionHandler(IEmployeeProjectionRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<NoContent>> Handle(ArchiveEmployeeProjectionCommand request, CancellationToken ct)
    {
        var tenantId = EmployeeProjectionGuards.RequireTenant(_tenantContext);
        var entity = await _repository.GetByIdAsync(tenantId, request.Id, ct);
        if (entity is null)
        {
            return Response<NoContent>.Fail("Employee projection was not found.", 404);
        }

        entity.IsDeleted = true;
        entity.DeletedAt = DateTimeOffset.UtcNow;
        entity.UpdatedAt = entity.DeletedAt;
        entity.ProjectionState = EmployeeProjectionState.Archived;
        await _repository.UpdateAsync(entity, ct);

        return Response<NoContent>.Success(204);
    }
}
