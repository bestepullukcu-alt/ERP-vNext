using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.LearningTraining.Queries;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.LearningTraining.Handlers;

public sealed class GetLearningTrainingReadinessListHandler
    : IRequestHandler<GetLearningTrainingReadinessListQuery, Response<IReadOnlyList<LearningTrainingReadinessListItemDto>>>
{
    private readonly ILearningTrainingReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetLearningTrainingReadinessListHandler(ILearningTrainingReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<IReadOnlyList<LearningTrainingReadinessListItemDto>>> Handle(
        GetLearningTrainingReadinessListQuery request,
        CancellationToken ct)
    {
        var tenant = LearningTrainingGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IReadOnlyList<LearningTrainingReadinessListItemDto>>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var rows = await _repository.ListAsync(tenant.Data, scope, ct);
        return Response<IReadOnlyList<LearningTrainingReadinessListItemDto>>.Success(rows.Select(LearningTrainingMapper.ToListItem).ToList());
    }
}

public sealed class GetLearningTrainingReadinessByIdHandler
    : IRequestHandler<GetLearningTrainingReadinessByIdQuery, Response<LearningTrainingReadinessDto>>
{
    private readonly ILearningTrainingReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetLearningTrainingReadinessByIdHandler(ILearningTrainingReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<LearningTrainingReadinessDto>> Handle(GetLearningTrainingReadinessByIdQuery request, CancellationToken ct)
    {
        var tenant = LearningTrainingGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<LearningTrainingReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        return entity is null
            ? Response<LearningTrainingReadinessDto>.Fail("LearningTraining readiness record was not found.", 404)
            : Response<LearningTrainingReadinessDto>.Success(LearningTrainingMapper.ToDto(entity));
    }
}

public sealed class GetLearningTrainingAuditMetadataHandler
    : IRequestHandler<GetLearningTrainingAuditMetadataQuery, Response<LearningTrainingAuditMetadataDto>>
{
    private readonly ILearningTrainingReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetLearningTrainingAuditMetadataHandler(ILearningTrainingReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<LearningTrainingAuditMetadataDto>> Handle(GetLearningTrainingAuditMetadataQuery request, CancellationToken ct)
    {
        var tenant = LearningTrainingGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<LearningTrainingAuditMetadataDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        return entity is null
            ? Response<LearningTrainingAuditMetadataDto>.Fail("LearningTraining readiness record was not found.", 404)
            : Response<LearningTrainingAuditMetadataDto>.Success(LearningTrainingMapper.ToAuditMetadata(entity));
    }
}
