using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.ReviewBoard.Queries;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.ReviewBoard.Handlers;

public sealed class GetReviewBoardCaseListHandler
    : IRequestHandler<GetReviewBoardCaseListQuery, Response<IReadOnlyList<ReviewBoardCaseListItemDto>>>
{
    private readonly ITepReviewBoardCaseMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetReviewBoardCaseListHandler(ITepReviewBoardCaseMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<IReadOnlyList<ReviewBoardCaseListItemDto>>> Handle(GetReviewBoardCaseListQuery request, CancellationToken ct)
    {
        var tenant = ReviewBoardGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IReadOnlyList<ReviewBoardCaseListItemDto>>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var items = await _repository.ListAsync(tenant.Data, ct);
        return Response<IReadOnlyList<ReviewBoardCaseListItemDto>>.Success(items.Select(ReviewBoardMapper.ToListItemDto).ToList());
    }
}

public sealed class GetReviewBoardCaseByIdHandler
    : IRequestHandler<GetReviewBoardCaseByIdQuery, Response<ReviewBoardCaseDto>>
{
    private readonly ITepReviewBoardCaseMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetReviewBoardCaseByIdHandler(ITepReviewBoardCaseMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<ReviewBoardCaseDto>> Handle(GetReviewBoardCaseByIdQuery request, CancellationToken ct)
    {
        var tenant = ReviewBoardGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<ReviewBoardCaseDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        return entity is null
            ? Response<ReviewBoardCaseDto>.Fail("Review-board case was not found.", 404)
            : Response<ReviewBoardCaseDto>.Success(ReviewBoardMapper.ToDto(entity));
    }
}

public sealed class GetReviewBoardCaseAuditMetadataHandler
    : IRequestHandler<GetReviewBoardCaseAuditMetadataQuery, Response<ReviewBoardAuditMetadataDto>>
{
    private readonly ITepReviewBoardCaseMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetReviewBoardCaseAuditMetadataHandler(ITepReviewBoardCaseMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<ReviewBoardAuditMetadataDto>> Handle(GetReviewBoardCaseAuditMetadataQuery request, CancellationToken ct)
    {
        var tenant = ReviewBoardGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<ReviewBoardAuditMetadataDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        return entity is null
            ? Response<ReviewBoardAuditMetadataDto>.Fail("Review-board case was not found.", 404)
            : Response<ReviewBoardAuditMetadataDto>.Success(new ReviewBoardAuditMetadataDto(
                entity.Id,
                entity.AuditEvidenceState,
                entity.RetentionState,
                entity.DependencyStates.Select(ReviewBoardMapper.ToDto).ToList(),
                entity.DeferredReason));
    }
}
