using System.Globalization;
using System.Security.Cryptography;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Recall.Commands;
using Diten.InventoryService.Application.Features.Recall.Evidence;
using Diten.InventoryService.Application.Features.Recall.Ports;
using Diten.InventoryService.Application.Features.Recall.Queries;
using Diten.InventoryService.Application.Features.Recall.Services;
using Diten.InventoryService.Domain.Recall.Entities;
using Diten.InventoryService.Domain.Recall.Repositories;
using Diten.InventoryService.Domain.Recall.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.Recall.Handlers;

/// <summary>The wire shapes of the Kanıt Paketi (RECALL 1.7.0).</summary>
public static class RecallEvidenceMapping
{
    public static RecallEvidenceSubjectDto Subject(RecallEvidenceSubjectKind kind, Guid? caseId, Guid? runId, DateOnly? from, DateOnly? to, string label)
        => new(kind.ToString(), caseId?.ToString("D"), runId?.ToString("D"), Day(from), Day(to), label);

    public static RecallEvidencePreviewDto Preview(RecallEvidenceDocument d) => new(
        Subject(d.SubjectKind, d.CaseId, d.RunId, d.From, d.To, d.SubjectLabel),
        d.LegalEntityId.ToString("D"),
        RecallSupport.Wire(d.GeneratedAtUtc),
        d.Language,
        d.Sections.ToDictionary(s => s.Key, s => new RecallEvidenceSectionDto(s.Applicable, s.Note,
            s.Tables.Select(t => new RecallEvidenceTableDto(t.Key, t.Columns, t.Rows)).ToList()), StringComparer.Ordinal));

    public static RecallEvidenceExportDto Export(RecallEvidenceExport e) => new(
        e.Id.ToString("D"),
        Subject(e.SubjectKind, e.CaseId, e.RunId, e.PeriodFrom, e.PeriodTo, e.SubjectLabel),
        e.Format.ToString(),
        e.FileName,
        e.Sha256,
        e.SizeBytes,
        new RecallUserAtDto(e.ExportedBy.ToString("D"), RecallSupport.Wire(e.ExportedAtUtc)),
        e.Sections,
        e.DmsRef);

    /// <summary>Lower-case hex SHA-256 of the file bytes.</summary>
    public static string Sha256Hex(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static string? Day(DateOnly? d) => d?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}

/// <summary>previewEvidence — the seven sections, exactly what the PDF / CSV of the same subject print.</summary>
public sealed class PreviewRecallEvidenceHandler : IRequestHandler<PreviewRecallEvidenceQuery, Response<RecallEvidencePreviewDto>>
{
    private readonly RecallEvidenceBuilder _builder;

    public PreviewRecallEvidenceHandler(RecallEvidenceBuilder builder) => _builder = builder;

    public async Task<Response<RecallEvidencePreviewDto>> Handle(PreviewRecallEvidenceQuery request, CancellationToken ct)
    {
        var (document, code, error, status) = await _builder.BuildAsync(request.CaseId, request.RunId, request.From, request.To, request.Language, ct);
        return document is null
            ? Response<RecallEvidencePreviewDto>.Fail(code!, error!, status)
            : Response<RecallEvidencePreviewDto>.Success(RecallEvidenceMapping.Preview(document));
    }
}

/// <summary>
/// createEvidenceExport (pack §4.13, D-12) — builds the pack, renders it (PDF / CSV), hashes THE PRODUCED BYTES and stores the bytes with the log
/// row and the audit row <c>recall.evidence.exported</c> in one transaction (BL-501: the central record joins it). The same key + the same
/// request answers the stored export (the same file and hash); the same key + another request → 400 IDEMPOTENCY_KEY_REUSED.
/// </summary>
public sealed class CreateRecallEvidenceExportHandler : IRequestHandler<CreateRecallEvidenceExportCommand, Response<RecallEvidenceExportDto>>
{
    private readonly IRecallRepository _repository;
    private readonly RecallEvidenceBuilder _builder;
    private readonly IRecallEvidencePdfRenderer _pdf;
    private readonly RecallSupport _support;

    public CreateRecallEvidenceExportHandler(IRecallRepository repository, RecallEvidenceBuilder builder, IRecallEvidencePdfRenderer pdf, RecallSupport support)
    {
        _repository = repository;
        _builder = builder;
        _pdf = pdf;
        _support = support;
    }

    public async Task<Response<RecallEvidenceExportDto>> Handle(CreateRecallEvidenceExportCommand request, CancellationToken ct)
    {
        if (request.Format is not ("Pdf" or "Csv")) return RecallSupport.Invalid<RecallEvidenceExportDto>("format must be Pdf or Csv.");
        var format = request.Format == "Csv" ? RecallEvidenceFormat.Csv : RecallEvidenceFormat.Pdf;
        var fingerprint = RecallSupport.Fingerprint(("op", "createEvidenceExport"), ("caseId", request.CaseId), ("runId", request.RunId), ("from", request.From),
            ("to", request.To), ("format", request.Format));
        if (await _repository.FindEvidenceExportByKeyAsync(_support.UserId, request.IdempotencyKey, ct) is { } earlier)
        {
            return earlier.RequestFingerprint == fingerprint
                ? Response<RecallEvidenceExportDto>.Success(RecallEvidenceMapping.Export(earlier))
                : RecallSupport.KeyReused<RecallEvidenceExportDto>();
        }

        var (document, code, error, status) = await _builder.BuildAsync(request.CaseId, request.RunId, request.From, request.To, request.Language, ct);
        if (document is null) return Response<RecallEvidenceExportDto>.Fail(code!, error!, status);

        var bytes = format == RecallEvidenceFormat.Csv ? RecallEvidenceCsv.Render(document) : _pdf.Render(document);
        var now = _support.Now();
        var id = Guid.NewGuid();
        var stem = new string(document.SubjectLabel.Select(ch => char.IsLetterOrDigit(ch) || ch == '-' ? ch : '_').ToArray());
        var export = new RecallEvidenceExport
        {
            Id = id,
            CreatedAt = now,
            SubjectKind = document.SubjectKind,
            CaseId = document.CaseId,
            RunId = document.RunId,
            PeriodFrom = document.From,
            PeriodTo = document.To,
            SubjectLabel = document.SubjectLabel,
            Format = format,
            FileName = $"{stem}_kanit_{id.ToString("N")[..8]}.{(format == RecallEvidenceFormat.Csv ? "csv" : "pdf")}",
            ContentType = format == RecallEvidenceFormat.Csv ? "text/csv; charset=utf-8" : "application/pdf",
            Sha256 = RecallEvidenceMapping.Sha256Hex(bytes),
            SizeBytes = bytes.LongLength,
            Sections = document.SectionKeys.ToList(),
            Language = document.Language,
            ExportedBy = _support.UserId,
            ExportedAtUtc = now,
            DmsRef = null,
            IdempotencyKey = request.IdempotencyKey,
            RequestFingerprint = fingerprint
        };
        var file = new RecallEvidenceFile { Id = id, CreatedAt = now, Content = bytes };
        try
        {
            await _repository.InTransactionAsync<bool>(async (session, tx) =>
            {
                await session.InsertEvidenceExportAsync(export, file, tx);
                await session.InsertAuditAsync(_support.Audit(RecallContract.AuditEvidenceExported, RecallContract.SubjectEvidenceExport, id, document.SubjectLabel, now,
                    request.IdempotencyKey, new Dictionary<string, string?>
                    {
                        ["subject"] = document.SubjectKind.ToString(),
                        ["subjectLabel"] = document.SubjectLabel,
                        ["format"] = request.Format,
                        ["fileName"] = export.FileName,
                        ["sha256"] = export.Sha256,
                        ["sizeBytes"] = export.SizeBytes.ToString(CultureInfo.InvariantCulture),
                        ["sections"] = string.Join(',', export.Sections)
                    }), tx);
                return true;
            }, ct);
        }
        catch (RecallDuplicateException)
        {
            return await _repository.FindEvidenceExportByKeyAsync(_support.UserId, request.IdempotencyKey, ct) is { } twin && twin.RequestFingerprint == fingerprint
                ? Response<RecallEvidenceExportDto>.Success(RecallEvidenceMapping.Export(twin))
                : RecallSupport.KeyReused<RecallEvidenceExportDto>();
        }

        return Response<RecallEvidenceExportDto>.Success(RecallEvidenceMapping.Export(export), 201);
    }
}

/// <summary>listEvidenceExports — the company's export log, newest first.</summary>
public sealed class ListRecallEvidenceExportsHandler : IRequestHandler<ListRecallEvidenceExportsQuery, Response<RecallEvidenceExportPageDto>>
{
    private readonly IRecallRepository _repository;

    public ListRecallEvidenceExportsHandler(IRecallRepository repository) => _repository = repository;

    public async Task<Response<RecallEvidenceExportPageDto>> Handle(ListRecallEvidenceExportsQuery request, CancellationToken ct)
    {
        if (RecallPaging.Parse(request.Page, request.PageSize) is not { } paging)
            return RecallSupport.Invalid<RecallEvidenceExportPageDto>($"page must be ≥ 1 and pageSize 1-{RecallContract.MaxPageSize}.");
        var (items, total) = await _repository.ListEvidenceExportsAsync(paging.Page, paging.PageSize, ct);
        return Response<RecallEvidenceExportPageDto>.Success(new RecallEvidenceExportPageDto(items.Select(RecallEvidenceMapping.Export).ToList(), total, paging.Page, paging.PageSize));
    }
}

/// <summary>getEvidenceFile — the STORED bytes (what was hashed); unknown → 404 (another company → the controller answers 403).</summary>
public sealed class GetRecallEvidenceFileHandler : IRequestHandler<GetRecallEvidenceFileQuery, Response<RecallEvidenceFileDto>>
{
    private readonly IRecallRepository _repository;

    public GetRecallEvidenceFileHandler(IRecallRepository repository) => _repository = repository;

    public async Task<Response<RecallEvidenceFileDto>> Handle(GetRecallEvidenceFileQuery request, CancellationToken ct)
    {
        if (!RecallSupport.TryParseId(request.ExportId, out var id)
            || await _repository.FindEvidenceExportAsync(id, ct) is not { } export
            || await _repository.FindEvidenceFileAsync(id, ct) is not { } file)
        {
            return Response<RecallEvidenceFileDto>.Fail(RecallErrorCodes.EvidenceExportNotFound, "Evidence export not found.", 404);
        }

        return Response<RecallEvidenceFileDto>.Success(new RecallEvidenceFileDto(file.Content, export.ContentType, export.FileName, export.Sha256));
    }
}
