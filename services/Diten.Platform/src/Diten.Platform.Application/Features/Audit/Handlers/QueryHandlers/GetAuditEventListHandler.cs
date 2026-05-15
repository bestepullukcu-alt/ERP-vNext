using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Audit.Queries;
using Diten.Platform.Application.Features.Audit.Services;
using Diten.Platform.Application.Features.Tenants;
using Diten.Platform.Domain.Entities.Audit;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Audit.Handlers.QueryHandlers;

public sealed class GetAuditEventListHandler
    : IRequestHandler<GetAuditEventListQuery, Response<PagedResult<AuditEventListItemDto>>>
{
    private readonly IAuditEventRepository _repository;
    private readonly IAuditMetaAuditWriter _metaAuditWriter;

    public GetAuditEventListHandler(
        IAuditEventRepository repository,
        IAuditMetaAuditWriter metaAuditWriter)
    {
        _repository = repository;
        _metaAuditWriter = metaAuditWriter;
    }

    public async Task<Response<PagedResult<AuditEventListItemDto>>> Handle(
        GetAuditEventListQuery request,
        CancellationToken ct)
    {
        if (!AuditFilterParser.TryBuildListSearchRequest(request.Filter, out var search, out var error))
        {
            return Response<PagedResult<AuditEventListItemDto>>.Fail(error!, 400);
        }

        var result = await _repository.SearchForPlatformCrossTenantAsync(search, ct);
        await _metaAuditWriter.WriteAsync(new AuditMetaAuditRequest(
            "PlatformAudit.GetAuditEventListQuery",
            AuditCategory.DataPrivacy,
            AuditOperation.Execute,
            AuditOutcome.Succeeded,
            "AuditEvent",
            null,
            search.TenantId ?? AuditTenantIds.PlatformSystemTenantId,
            new Dictionary<string, object?>
            {
                ["tenantId"] = search.TenantId,
                ["targetTenantId"] = search.TargetTenantId,
                ["category"] = search.Category?.ToString(),
                ["operation"] = search.Operation?.ToString(),
                ["outcome"] = search.Outcome?.ToString(),
                ["page"] = Math.Max(request.Filter.Page, 1),
                ["pageSize"] = Math.Clamp(request.Filter.PageSize, 1, 500),
                ["resultCount"] = result.Items.Count,
                ["totalCount"] = result.TotalCount
            }), ct);

        return Response<PagedResult<AuditEventListItemDto>>.Success(
            AuditEventMapper.ToPagedResult(result.Items, request.Filter.Page, request.Filter.PageSize, result.TotalCount));
    }
}
