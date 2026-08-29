using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.StrategyTemplate.Queries;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.StrategyTemplate.Handlers.QueryHandlers;

/// <summary>Strategy template grid. Filtering and ordering happen in memory over the tenant rows, so no DateTimeOffset
/// field ever becomes a Mongo sort key (they are stored as BSON arrays and sorting two of them together is the
/// parallel-array trap). Ordering is (code, business version), which is stable and reads the way an author thinks.</summary>
public sealed class ListStrategyTemplatesHandler
    : IRequestHandler<ListStrategyTemplatesQuery, Response<StrategyTemplateListDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IStrategyTemplateRepository _templates;

    public ListStrategyTemplatesHandler(ITenantContext tenant, IStrategyTemplateRepository templates)
    {
        _tenant = tenant;
        _templates = templates;
    }

    public async Task<Response<StrategyTemplateListDto>> Handle(
        ListStrategyTemplatesQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<StrategyTemplateListDto>.Fail("Tenant context is required.", 400);
        }

        var rows = await _templates.ListAsync(tenantId, cancellationToken);

        var filtered = rows.Where(t => request.IncludeArchived || !t.IsArchived());

        if (!string.IsNullOrWhiteSpace(request.TemplateStatus))
        {
            var status = StrategyTemplateStatuses.Normalize(request.TemplateStatus);
            filtered = filtered.Where(t => string.Equals(t.TemplateStatus, status, StringComparison.Ordinal));
        }

        if (!string.IsNullOrWhiteSpace(request.SubjectType))
        {
            var subject = StrategyTemplateSubjectTypes.Normalize(request.SubjectType);
            filtered = filtered.Where(t => string.Equals(t.SubjectType, subject, StringComparison.Ordinal));
        }

        if (!string.IsNullOrWhiteSpace(request.BusinessUnitId))
        {
            filtered = filtered.Where(t => string.Equals(
                t.BusinessUnitId, request.BusinessUnitId.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(request.TemplateCode))
        {
            filtered = filtered.Where(t => string.Equals(
                t.TemplateCode, request.TemplateCode.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        // The reverse question, answered WITHOUT touching a single member of the segment.
        if (request.SegmentId is { } segmentId && segmentId != Guid.Empty)
        {
            filtered = filtered.Where(t => t.SegmentBindings.Any(b => b.SegmentId == segmentId));
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            filtered = filtered.Where(t =>
                t.TemplateCode.Contains(search, StringComparison.OrdinalIgnoreCase)
                || t.TemplateName.Contains(search, StringComparison.OrdinalIgnoreCase)
                || (t.Description ?? string.Empty).Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        var items = filtered
            .OrderBy(t => t.TemplateCode, StringComparer.OrdinalIgnoreCase)
            .ThenBy(t => t.TemplateVersion)
            .Select(StrategyTemplateMapper.ToListItem)
            .ToList();

        return Response<StrategyTemplateListDto>.Success(new StrategyTemplateListDto(items, items.Count));
    }
}
