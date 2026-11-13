using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.CountEvidenceExports.Handlers.CommandHandlers;
using Diten.InventoryService.Application.Features.CountEvidenceExports.Queries;
using Diten.InventoryService.Application.Features.CycleCounting;
using Diten.InventoryService.Application.Features.CycleCounting.Services;
using Diten.InventoryService.Domain.CycleCounting.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.InventoryService.Application.Features.CountEvidenceExports.Handlers.QueryHandlers;

/// <summary><c>listCountEvidenceExports</c> (draft §4.4) — the caller's company's export log, newest first (paging 1–200); <c>countId</c> narrows it.</summary>
public sealed class GetCountEvidenceExportListHandler : IRequestHandler<GetCountEvidenceExportListQuery, Response<CountEvidenceExportPageDto>>
{
    private readonly ICycleCountRepository _repository;

    public GetCountEvidenceExportListHandler(ICycleCountRepository repository) => _repository = repository;

    public async Task<Response<CountEvidenceExportPageDto>> Handle(GetCountEvidenceExportListQuery request, CancellationToken ct)
    {
        if (CycleCountPaging.Parse(request.Page, request.PageSize) is not { } paging)
            return CycleCountSupport.Invalid<CountEvidenceExportPageDto>($"page must be ≥ 1 and pageSize 1-{CycleCountContract.MaxPageSize}.");
        Guid? countId = null;
        if (!string.IsNullOrWhiteSpace(request.CountId))
        {
            if (!CycleCountSupport.TryParseId(request.CountId, out var id)) return CycleCountSupport.Invalid<CountEvidenceExportPageDto>("countId must be a UUID.");
            countId = id;
        }

        var result = await _repository.ListEvidenceExportsAsync(countId, paging.Page, paging.PageSize, ct);
        return Response<CountEvidenceExportPageDto>.Success(new CountEvidenceExportPageDto(
            result.Items.Select(CreateCountEvidenceExportHandler.Map).ToList(), result.Total, paging.Page, paging.PageSize));
    }
}

/// <summary>
/// <c>getCountEvidenceExportFile</c> (draft §4.4, AC-43, AC-44) — the STORED bytes of an export of the caller's tenant + company (another one's is 404
/// COUNT_EVIDENCE_NOT_FOUND). The bytes are re-hashed before they leave: a file whose hash is not the recorded one is never served (500
/// COUNT_EVIDENCE_INTEGRITY and an error log line). Every download is logged (who, which export, its hash; pack §21 names the export as the
/// central record — the download is a read).
/// </summary>
public sealed class GetCountEvidenceExportFileHandler : IRequestHandler<GetCountEvidenceExportFileQuery, Response<CountEvidenceFileDto>>
{
    private readonly ICycleCountRepository _repository;
    private readonly CycleCountSupport _support;
    private readonly ILogger<GetCountEvidenceExportFileHandler> _logger;

    public GetCountEvidenceExportFileHandler(ICycleCountRepository repository, CycleCountSupport support, ILogger<GetCountEvidenceExportFileHandler> logger)
    {
        _repository = repository;
        _support = support;
        _logger = logger;
    }

    public async Task<Response<CountEvidenceFileDto>> Handle(GetCountEvidenceExportFileQuery request, CancellationToken ct)
    {
        if (await _repository.FindEvidenceExportAsync(request.ExportId, ct) is not { } export
            || await _repository.FindEvidenceFileAsync(request.ExportId, ct) is not { } file)
        {
            return NotFound();
        }

        var hash = CreateCountEvidenceExportHandler.Sha256Hex(file.Content);
        if (!string.Equals(hash, export.Sha256, StringComparison.Ordinal))
        {
            _logger.LogError("Count evidence export {ExportId} of tenant {TenantId}: the stored file's SHA-256 is not the recorded one; not served.",
                export.Id, _support.TenantId);
            return CycleCountSupport.Fail<CountEvidenceFileDto>(CycleCountErrorCodes.EvidenceIntegrity, "The stored evidence file does not match its recorded hash.", 500);
        }

        _logger.LogInformation("Count evidence export {ExportId} (count {CountId}, sha256 {Sha256}) downloaded by user {UserId} of tenant {TenantId} / company {LegalEntityId}.",
            export.Id, export.CountId, export.Sha256, _support.UserId, _support.TenantId, _support.LegalEntityId);
        return Response<CountEvidenceFileDto>.Success(new CountEvidenceFileDto(file.Content, export.ContentType, export.FileName, export.Sha256));
    }

    public static Response<CountEvidenceFileDto> NotFound()
        => CycleCountSupport.Fail<CountEvidenceFileDto>(CycleCountErrorCodes.EvidenceNotFound, "Evidence export not found.", 404);
}
