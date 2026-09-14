using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.AssociationOperations.Queries;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.AssociationOperations.Handlers;

public sealed class GetAssociationOperationsReadinessListHandler
    : IRequestHandler<GetAssociationOperationsReadinessListQuery, Response<IReadOnlyList<AssociationOperationsReadinessListItemDto>>>
{
    private readonly IAssociationOperationsReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetAssociationOperationsReadinessListHandler(IAssociationOperationsReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<IReadOnlyList<AssociationOperationsReadinessListItemDto>>> Handle(
        GetAssociationOperationsReadinessListQuery request,
        CancellationToken ct)
    {
        var tenant = AssociationOperationsGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IReadOnlyList<AssociationOperationsReadinessListItemDto>>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var rows = await _repository.ListAsync(tenant.Data, scope, ct);
        return Response<IReadOnlyList<AssociationOperationsReadinessListItemDto>>.Success(rows.Select(AssociationOperationsMapper.ToListItem).ToList());
    }
}

public sealed class GetAssociationOperationsReadinessByIdHandler
    : IRequestHandler<GetAssociationOperationsReadinessByIdQuery, Response<AssociationOperationsReadinessDto>>
{
    private readonly IAssociationOperationsReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetAssociationOperationsReadinessByIdHandler(IAssociationOperationsReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<AssociationOperationsReadinessDto>> Handle(GetAssociationOperationsReadinessByIdQuery request, CancellationToken ct)
    {
        var tenant = AssociationOperationsGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<AssociationOperationsReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        return entity is null
            ? Response<AssociationOperationsReadinessDto>.Fail("AssociationOperations readiness record was not found.", 404)
            : Response<AssociationOperationsReadinessDto>.Success(AssociationOperationsMapper.ToDto(entity));
    }
}

public sealed class GetAssociationOperationsAuditMetadataHandler
    : IRequestHandler<GetAssociationOperationsAuditMetadataQuery, Response<AssociationOperationsAuditMetadataDto>>
{
    private readonly IAssociationOperationsReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetAssociationOperationsAuditMetadataHandler(IAssociationOperationsReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<AssociationOperationsAuditMetadataDto>> Handle(GetAssociationOperationsAuditMetadataQuery request, CancellationToken ct)
    {
        var tenant = AssociationOperationsGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<AssociationOperationsAuditMetadataDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        return entity is null
            ? Response<AssociationOperationsAuditMetadataDto>.Fail("AssociationOperations readiness record was not found.", 404)
            : Response<AssociationOperationsAuditMetadataDto>.Success(AssociationOperationsMapper.ToAuditMetadata(entity));
    }
}
