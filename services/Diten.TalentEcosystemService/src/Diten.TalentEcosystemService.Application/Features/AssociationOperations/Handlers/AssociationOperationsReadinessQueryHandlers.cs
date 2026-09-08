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

    public GetAssociationOperationsReadinessListHandler(IAssociationOperationsReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
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

        var rows = await _repository.ListAsync(tenant.Data, ct);
        return Response<IReadOnlyList<AssociationOperationsReadinessListItemDto>>.Success(rows.Select(AssociationOperationsMapper.ToListItem).ToList());
    }
}

public sealed class GetAssociationOperationsReadinessByIdHandler
    : IRequestHandler<GetAssociationOperationsReadinessByIdQuery, Response<AssociationOperationsReadinessDto>>
{
    private readonly IAssociationOperationsReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetAssociationOperationsReadinessByIdHandler(IAssociationOperationsReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<AssociationOperationsReadinessDto>> Handle(GetAssociationOperationsReadinessByIdQuery request, CancellationToken ct)
    {
        var tenant = AssociationOperationsGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<AssociationOperationsReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
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

    public GetAssociationOperationsAuditMetadataHandler(IAssociationOperationsReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<AssociationOperationsAuditMetadataDto>> Handle(GetAssociationOperationsAuditMetadataQuery request, CancellationToken ct)
    {
        var tenant = AssociationOperationsGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<AssociationOperationsAuditMetadataDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        return entity is null
            ? Response<AssociationOperationsAuditMetadataDto>.Fail("AssociationOperations readiness record was not found.", 404)
            : Response<AssociationOperationsAuditMetadataDto>.Success(AssociationOperationsMapper.ToAuditMetadata(entity));
    }
}
