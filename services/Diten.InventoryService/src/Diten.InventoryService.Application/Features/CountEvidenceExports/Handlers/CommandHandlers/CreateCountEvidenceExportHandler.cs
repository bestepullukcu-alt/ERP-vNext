using System.Security.Cryptography;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.CountEvidenceExports.Commands;
using Diten.InventoryService.Application.Features.CycleCounting;
using Diten.InventoryService.Application.Features.CycleCounting.Ports;
using Diten.InventoryService.Application.Features.CycleCounting.Services;
using Diten.InventoryService.Domain.CycleCounting.Entities;
using Diten.InventoryService.Domain.CycleCounting.Enums;
using Diten.InventoryService.Domain.CycleCounting.Repositories;
using MediatR;

namespace Diten.InventoryService.Application.Features.CountEvidenceExports.Handlers.CommandHandlers;

/// <summary>
/// DL-5 <c>createCountEvidenceExport</c> (pack §4.5, AC-43; the MOD-0176 <c>CreateExpiryEvidenceExportHandler</c> pattern, an own copy):
/// <list type="number">
/// <item>format <c>Pdf</c> | <c>Csv</c> (else 400); the count of the caller's tenant + company (else 404 COUNT_NOT_FOUND); Submitted or Closed —
/// the evidence of a count whose variances exist (else 409 COUNT_INVALID_STATE);</item>
/// <item>the caller's key: the same key with the same request answers the stored export (200, no new file); with another request 400
/// IDEMPOTENCY_KEY_REUSED;</item>
/// <item>the language: the requested one (one of the 7, else en); a PDF is drawn in a language its font draws (zh / ar → en, the requested code kept in
/// <c>RequestedLanguage</c> and a note at the top); the CSV in the requested one;</item>
/// <item>the bytes are hashed and stored WITH the record in one transaction (insert-only; the central record commits with it — Affected = 1).</item>
/// </list>
/// </summary>
public sealed class CreateCountEvidenceExportHandler : IRequestHandler<CreateCountEvidenceExportCommand, Response<CountEvidenceExportDto>>
{
    private readonly ICycleCountRepository _repository;
    private readonly CountEvidenceDocumentBuilder _builder;
    private readonly ICycleCountEvidencePdfRenderer _pdf;
    private readonly CycleCountSupport _support;

    public CreateCountEvidenceExportHandler(ICycleCountRepository repository, CountEvidenceDocumentBuilder builder, ICycleCountEvidencePdfRenderer pdf,
        CycleCountSupport support)
    {
        _repository = repository;
        _builder = builder;
        _pdf = pdf;
        _support = support;
    }

    public async Task<Response<CountEvidenceExportDto>> Handle(CreateCountEvidenceExportCommand request, CancellationToken ct)
    {
        if (request.Format is not ("Pdf" or "Csv")) return CycleCountSupport.Invalid<CountEvidenceExportDto>("format must be Pdf or Csv.");
        var format = request.Format == "Csv" ? CountEvidenceFormat.Csv : CountEvidenceFormat.Pdf;
        var requested = CycleCountEvidenceText.Normal(request.Language);
        var fingerprint = CycleCountSupport.Fingerprint(("op", CycleCountContract.OpCreateCountEvidenceExport), ("countId", request.CountId.ToString("D")),
            ("format", request.Format), ("language", requested));
        if (await _repository.FindEvidenceExportByKeyAsync(_support.UserId, request.IdempotencyKey, ct) is { } earlier)
        {
            return earlier.RequestFingerprint == fingerprint
                ? Response<CountEvidenceExportDto>.Success(Map(earlier))
                : CycleCountSupport.KeyReused<CountEvidenceExportDto>();
        }

        if (await _repository.FindAsync(request.CountId, ct) is not { } count) return CycleCountSupport.NotFound<CountEvidenceExportDto>();
        if (count.Status is not (CycleCountStatus.Submitted or CycleCountStatus.Closed)) return CycleCountSupport.InvalidState<CountEvidenceExportDto>(count.Status);

        var now = _support.Now();
        var language = format == CountEvidenceFormat.Pdf ? CycleCountEvidenceText.PdfLanguage(requested) : requested;
        var document = await _builder.BuildAsync(count, language, requested, now, ct);
        var bytes = format == CountEvidenceFormat.Csv ? CountEvidenceCsv.Render(document) : _pdf.Render(document);
        var id = Guid.NewGuid();
        var extension = format == CountEvidenceFormat.Csv ? "csv" : "pdf";
        var export = new CountEvidenceExport
        {
            Id = id,
            CreatedAt = now,
            CountId = count.Id,
            CountNumber = count.Number,
            Format = format,
            Language = language,
            RequestedLanguage = requested,
            RequestedBy = _support.UserId,
            RequestedAt = now,
            FileId = id,
            Sha256 = Sha256Hex(bytes),
            Status = CountEvidenceStatus.Ready,
            FileName = $"count-evidence-{count.Number}-{language}-{now:yyyyMMddHHmmss}.{extension}",
            ContentType = format == CountEvidenceFormat.Csv ? "text/csv; charset=utf-8" : "application/pdf",
            SizeBytes = bytes.LongLength,
            IdempotencyKey = request.IdempotencyKey,
            RequestFingerprint = fingerprint,
            Version = 1
        };
        var file = new CountEvidenceFile { Id = id, CreatedAt = now, ExportId = id, Content = bytes, Version = 1 };
        try
        {
            await _repository.InTransactionAsync(async (session, tx) =>
            {
                await session.InsertEvidenceExportAsync(export, file, tx);
                return new CycleCountWriteOutcome(1, id);
            }, ct);
        }
        catch (CycleCountDuplicateException ex) when (ex.Index == CycleCountUniqueIndex.EvidenceKey)
        {
            return await _repository.FindEvidenceExportByKeyAsync(_support.UserId, request.IdempotencyKey, ct) is { } twin && twin.RequestFingerprint == fingerprint
                ? Response<CountEvidenceExportDto>.Success(Map(twin))
                : CycleCountSupport.KeyReused<CountEvidenceExportDto>();
        }

        return Response<CountEvidenceExportDto>.Success(Map(export), 201);
    }

    /// <summary>Lower-case hex SHA-256 of the file bytes.</summary>
    public static string Sha256Hex(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    public static CountEvidenceExportDto Map(CountEvidenceExport e) => new()
    {
        ExportId = e.Id.ToString("D"),
        CountId = e.CountId.ToString("D"),
        CountNumber = e.CountNumber,
        Format = e.Format.ToString(),
        Language = e.Language,
        RequestedLanguage = e.RequestedLanguage,
        RequestedBy = e.RequestedBy.ToString("D"),
        RequestedAt = CycleCountSupport.Wire(e.RequestedAt),
        FileName = e.FileName,
        Sha256 = e.Sha256,
        SizeBytes = e.SizeBytes,
        Status = e.Status.ToString()
    };
}
