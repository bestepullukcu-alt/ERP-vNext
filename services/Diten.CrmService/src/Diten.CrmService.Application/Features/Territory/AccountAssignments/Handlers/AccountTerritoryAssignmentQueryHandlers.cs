using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Territory.AccountAssignments.Handlers;

public sealed class GetAccountTerritoryAssignmentListHandler
    : IRequestHandler<GetAccountTerritoryAssignmentListQuery, Response<AccountTerritoryAssignmentListDto>>
{
    private readonly ITenantContext _tenant; private readonly ITerritoryModelRepository _models;
    private readonly IAccountTerritoryAssignmentRepository _assignments;
    public GetAccountTerritoryAssignmentListHandler(ITenantContext tenant, ITerritoryModelRepository models, IAccountTerritoryAssignmentRepository assignments)
        => (_tenant, _models, _assignments) = (tenant, models, assignments);
    public async Task<Response<AccountTerritoryAssignmentListDto>> Handle(GetAccountTerritoryAssignmentListQuery request, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId) return Response<AccountTerritoryAssignmentListDto>.Fail("Tenant context is required.", 400);
        if (await _models.GetByIdAsync(tenantId, request.ModelId, ct) is null) return Response<AccountTerritoryAssignmentListDto>.Fail("Territory model not found.", 404);
        var items = await _assignments.ListByModelAsync(tenantId, request.ModelId, ct);
        return Response<AccountTerritoryAssignmentListDto>.Success(new(items.Count, items.Select(AccountTerritoryAssignmentMapper.ToDto).ToList()));
    }
}

public sealed class GetAccountTerritoryAssignmentByIdHandler
    : IRequestHandler<GetAccountTerritoryAssignmentByIdQuery, Response<AccountTerritoryAssignmentDto>>
{
    private readonly ITenantContext _tenant; private readonly IAccountTerritoryAssignmentRepository _assignments;
    public GetAccountTerritoryAssignmentByIdHandler(ITenantContext tenant, IAccountTerritoryAssignmentRepository assignments)
        => (_tenant, _assignments) = (tenant, assignments);
    public async Task<Response<AccountTerritoryAssignmentDto>> Handle(GetAccountTerritoryAssignmentByIdQuery request, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId) return Response<AccountTerritoryAssignmentDto>.Fail("Tenant context is required.", 400);
        var item = await _assignments.GetByIdAsync(tenantId, request.ModelId, request.AssignmentId, ct);
        return item is null ? Response<AccountTerritoryAssignmentDto>.Fail("Account territory assignment not found.", 404)
            : Response<AccountTerritoryAssignmentDto>.Success(AccountTerritoryAssignmentMapper.ToDto(item));
    }
}

public sealed class GetAccountTerritoryAssignmentHistoryHandler
    : IRequestHandler<GetAccountTerritoryAssignmentHistoryQuery, Response<AccountTerritoryAssignmentListDto>>
{
    private readonly ITenantContext _tenant; private readonly ITerritoryAccountReader _accounts;
    private readonly IAccountTerritoryAssignmentRepository _assignments;
    public GetAccountTerritoryAssignmentHistoryHandler(ITenantContext tenant, ITerritoryAccountReader accounts, IAccountTerritoryAssignmentRepository assignments)
        => (_tenant, _accounts, _assignments) = (tenant, accounts, assignments);
    public async Task<Response<AccountTerritoryAssignmentListDto>> Handle(GetAccountTerritoryAssignmentHistoryQuery request, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId) return Response<AccountTerritoryAssignmentListDto>.Fail("Tenant context is required.", 400);
        if ((await _accounts.GetByIdsAsync(tenantId, new[] { request.AccountId }, ct)).Count == 0)
            return Response<AccountTerritoryAssignmentListDto>.Fail("Account not found.", 404);
        var items = await _assignments.ListByAccountAsync(tenantId, request.AccountId, ct);
        return Response<AccountTerritoryAssignmentListDto>.Success(new(items.Count, items.Select(AccountTerritoryAssignmentMapper.ToDto).ToList()));
    }
}

/// <summary>FU05A — current coverage is the intersection of an operationally valid territory model and an open
/// assignment at <c>effectiveAt</c> (<see cref="TerritoryCoverageLifecyclePolicy"/>). History is untouched: the rows
/// excluded here still come back in full from the history query.</summary>
public sealed class GetTerritoryCoverageSummaryHandler
    : IRequestHandler<GetTerritoryCoverageSummaryQuery, Response<TerritoryCoverageSummaryDto>>
{
    private readonly ITenantContext _tenant; private readonly ITerritoryAccountReader _accounts;
    private readonly IAccountTerritoryAssignmentRepository _assignments; private readonly ITerritoryModelRepository _models;
    public GetTerritoryCoverageSummaryHandler(ITenantContext tenant, ITerritoryAccountReader accounts,
        IAccountTerritoryAssignmentRepository assignments, ITerritoryModelRepository models)
        => (_tenant, _accounts, _assignments, _models) = (tenant, accounts, assignments, models);
    public async Task<Response<TerritoryCoverageSummaryDto>> Handle(GetTerritoryCoverageSummaryQuery request, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId) return Response<TerritoryCoverageSummaryDto>.Fail("Tenant context is required.", 400);
        if ((await _accounts.GetByIdsAsync(tenantId, new[] { request.AccountId }, ct)).Count == 0)
            return Response<TerritoryCoverageSummaryDto>.Fail("Account not found.", 404);
        var at = request.EffectiveAt ?? DateTimeOffset.UtcNow;
        var candidates = (await _assignments.ListByAccountAsync(tenantId, request.AccountId, ct))
            .Where(a => TerritoryCoverageLifecyclePolicy.IsAssignmentCurrent(a, at)).ToList();
        var models = await LoadModelsAsync(tenantId, candidates, ct);
        var current = TerritoryCoverageLifecyclePolicy.FilterCurrent(candidates, models, at)
            .Select(AccountTerritoryAssignmentMapper.ToDto).ToList();
        return Response<TerritoryCoverageSummaryDto>.Success(new(request.AccountId, at, current.Count > 0, current));
    }

    private async Task<IReadOnlyDictionary<Guid, Domain.Entities.TerritoryModel>> LoadModelsAsync(
        Guid tenantId, IReadOnlyList<Domain.Entities.AccountTerritoryAssignment> candidates, CancellationToken ct)
    {
        if (candidates.Count == 0) return new Dictionary<Guid, Domain.Entities.TerritoryModel>();
        var models = await _models.ListByIdsAsync(tenantId, TerritoryCoverageLifecyclePolicy.ModelIdsOf(candidates), ct);
        return models.ToDictionary(m => m.Id);
    }
}
