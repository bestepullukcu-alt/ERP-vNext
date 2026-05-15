using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.Audit.Queries;
using Diten.Platform.Application.Features.Audit.Services;
using Diten.Platform.Domain.Entities.Audit;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Audit.Handlers.QueryHandlers;

public sealed class ExportAuditEventsHandler : IRequestHandler<ExportAuditEventsQuery, Response<AuditExportResultDto>>
{
    private readonly IAuditEventRepository _repository;
    private readonly ISensitiveFieldRedactor _redactor;
    private readonly IAuditMetaAuditWriter _metaAuditWriter;

    public ExportAuditEventsHandler(
        IAuditEventRepository repository,
        ISensitiveFieldRedactor redactor,
        IAuditMetaAuditWriter metaAuditWriter)
    {
        _repository = repository;
        _redactor = redactor;
        _metaAuditWriter = metaAuditWriter;
    }

    public async Task<Response<AuditExportResultDto>> Handle(ExportAuditEventsQuery request, CancellationToken ct)
    {
        if (!AuditFilterParser.TryParseExportFormat(request.Request.Format, out var format, out var formatError))
        {
            return Response<AuditExportResultDto>.Fail(formatError!, 400);
        }

        if (!AuditFilterParser.TryBuildExportSearchRequest(request.Request, out var search, out var error))
        {
            return Response<AuditExportResultDto>.Fail(error!, 400);
        }

        var result = await _repository.SearchForPlatformCrossTenantAsync(search, ct);
        var content = format == AuditExportFormats.Json
            ? AuditExportSerializer.ToJson(result.Items, _redactor)
            : AuditExportSerializer.ToCsv(result.Items, _redactor);

        var exportedAt = DateTimeOffset.UtcNow;
        var fileName = $"audit-events-{exportedAt:yyyyMMddHHmmss}.{format}";
        var contentType = format == AuditExportFormats.Json
            ? "application/json"
            : "text/csv; charset=utf-8";

        await _metaAuditWriter.WriteAsync(new AuditMetaAuditRequest(
            "PlatformAudit.ExportAuditEventsQuery",
            AuditCategory.DataExport,
            AuditOperation.Export,
            AuditOutcome.Succeeded,
            "AuditEvent",
            null,
            search.TenantId ?? AuditTenantIds.PlatformSystemTenantId,
            new Dictionary<string, object?>
            {
                ["tenantId"] = search.TenantId,
                ["targetTenantId"] = search.TargetTenantId,
                ["format"] = format,
                ["rowCount"] = result.Items.Count,
                ["limit"] = request.Request.Limit,
                ["fromUtc"] = request.Request.FromUtc,
                ["toUtc"] = request.Request.ToUtc
            }), ct);

        return Response<AuditExportResultDto>.Success(new AuditExportResultDto(
            content,
            contentType,
            fileName,
            result.Items.Count));
    }
}
