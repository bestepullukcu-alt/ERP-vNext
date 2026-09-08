using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.HrDocumentation.Commands;
using Diten.HumanCapitalService.Domain.Enums;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.HrDocumentation.Handlers;

public sealed class DeleteHrDocumentationReadinessHandler : IRequestHandler<DeleteHrDocumentationReadinessCommand, Response<bool>>
{
    private readonly IHrDocumentationReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public DeleteHrDocumentationReadinessHandler(IHrDocumentationReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<bool>> Handle(DeleteHrDocumentationReadinessCommand request, CancellationToken ct)
    {
        var tenant = HrDocumentationGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<bool>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        if (entity is null)
        {
            return Response<bool>.Fail("HrDocumentation readiness record was not found.", 404);
        }

        var now = DateTimeOffset.UtcNow;
        entity.IsDeleted = true;
        entity.DeletedAt = now;
        entity.UpdatedAt = now;
        entity.HrDocumentationReadinessState = HrDocumentationReadinessState.Archived;

        await _repository.UpdateAsync(entity, ct);
        return Response<bool>.Success(204);
    }
}
