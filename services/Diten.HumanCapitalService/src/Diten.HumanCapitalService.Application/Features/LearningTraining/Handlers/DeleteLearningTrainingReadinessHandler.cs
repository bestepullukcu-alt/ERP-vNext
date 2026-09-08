using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.LearningTraining.Commands;
using Diten.HumanCapitalService.Domain.Enums;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.LearningTraining.Handlers;

public sealed class DeleteLearningTrainingReadinessHandler : IRequestHandler<DeleteLearningTrainingReadinessCommand, Response<bool>>
{
    private readonly ILearningTrainingReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public DeleteLearningTrainingReadinessHandler(ILearningTrainingReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<bool>> Handle(DeleteLearningTrainingReadinessCommand request, CancellationToken ct)
    {
        var tenant = LearningTrainingGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<bool>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        if (entity is null)
        {
            return Response<bool>.Fail("LearningTraining readiness record was not found.", 404);
        }

        var now = DateTimeOffset.UtcNow;
        entity.IsDeleted = true;
        entity.DeletedAt = now;
        entity.UpdatedAt = now;
        entity.LearningTrainingReadinessState = LearningTrainingReadinessState.Archived;

        await _repository.UpdateAsync(entity, ct);
        return Response<bool>.Success(204);
    }
}
