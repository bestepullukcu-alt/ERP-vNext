using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.ExitReferenceRecords.Queries;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.ExitReferenceRecords.Handlers;

public sealed class GetExitReferenceRecordListHandler
    : IRequestHandler<GetExitReferenceRecordListQuery, Response<IReadOnlyList<ExitReferenceRecordListItemDto>>>
{
    private readonly ITepExitReferenceRecordMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetExitReferenceRecordListHandler(ITepExitReferenceRecordMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<IReadOnlyList<ExitReferenceRecordListItemDto>>> Handle(GetExitReferenceRecordListQuery request, CancellationToken ct)
    {
        var tenant = ExitReferenceRecordGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IReadOnlyList<ExitReferenceRecordListItemDto>>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var items = await _repository.ListAsync(tenant.Data, ct);
        return Response<IReadOnlyList<ExitReferenceRecordListItemDto>>.Success(items.Select(ExitReferenceRecordMapper.ToListItemDto).ToList());
    }
}

public sealed class GetExitReferenceRecordByIdHandler
    : IRequestHandler<GetExitReferenceRecordByIdQuery, Response<ExitReferenceRecordDto>>
{
    private readonly ITepExitReferenceRecordMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetExitReferenceRecordByIdHandler(ITepExitReferenceRecordMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<ExitReferenceRecordDto>> Handle(GetExitReferenceRecordByIdQuery request, CancellationToken ct)
    {
        var tenant = ExitReferenceRecordGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<ExitReferenceRecordDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        return entity is null
            ? Response<ExitReferenceRecordDto>.Fail("Exit reference record was not found.", 404)
            : Response<ExitReferenceRecordDto>.Success(ExitReferenceRecordMapper.ToDto(entity));
    }
}

public sealed class GetExitReferenceRecordAuditMetadataHandler
    : IRequestHandler<GetExitReferenceRecordAuditMetadataQuery, Response<ExitReferenceAuditMetadataDto>>
{
    private readonly ITepExitReferenceRecordMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetExitReferenceRecordAuditMetadataHandler(ITepExitReferenceRecordMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<ExitReferenceAuditMetadataDto>> Handle(GetExitReferenceRecordAuditMetadataQuery request, CancellationToken ct)
    {
        var tenant = ExitReferenceRecordGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<ExitReferenceAuditMetadataDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        return entity is null
            ? Response<ExitReferenceAuditMetadataDto>.Fail("Exit reference record was not found.", 404)
            : Response<ExitReferenceAuditMetadataDto>.Success(new ExitReferenceAuditMetadataDto(
                entity.Id,
                entity.EvidenceRetentionState,
                entity.ReviewDisputeBoundaryState,
                entity.DependencyStates.Select(ExitReferenceRecordMapper.ToDto).ToList(),
                entity.DeferredReason));
    }
}
