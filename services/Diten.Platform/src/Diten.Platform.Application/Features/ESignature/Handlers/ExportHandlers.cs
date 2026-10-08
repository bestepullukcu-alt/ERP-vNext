using System.Globalization;
using System.Text;
using System.Text.Json;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Contracts.DocumentRepository;
using Diten.Platform.Application.Features.ESignature.Services;
using Diten.Platform.Domain.Entities.ESignature;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.ESignature.Handlers;

/// <summary>One row of an export: the record, its verification verdict, and (PDF only) the PNG bytes of a drawn signature.</summary>
public sealed record ESignatureExportRow(ESignatureRecord Record, string VerificationStatus, byte[]? Png);

public sealed record ESignatureExportDocument(string ExportNumber, string FilterText, string ByName, DateTime AtUtc, IReadOnlyList<ESignatureExportRow> Rows);

/// <summary>Server-side PDF (CT-D11: PDFsharp-MigraDoc). The file's SHA-256 is recorded in the export log.</summary>
public interface IESignaturePdfRenderer
{
    byte[] Render(ESignatureExportDocument document);
}

/// <summary>CSV (KS-6): UTF-8 with BOM, <c>;</c> separator, the mockup's columns. Cells are neutralized against spreadsheet formulas.</summary>
public static class ESignatureCsv
{
    public static readonly string[] Columns =
        ["Tarih-saat", "İmza kimliği", "İmzalayan", "Kullanıcı", "İmza anlamı", "Modül", "Kayıt", "Şirket", "Doğrulama"];

    public static byte[] Render(IReadOnlyList<ESignatureExportRow> rows)
    {
        var builder = new StringBuilder();
        builder.AppendJoin(';', Columns.Select(Cell)).Append("\r\n");
        foreach (var row in rows)
        {
            var r = row.Record;
            builder.AppendJoin(';', new[]
            {
                r.SignedAtUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " UTC",
                r.SignatureNumber,
                r.SignerNameAtSign,
                r.SignerLogin,
                $"{r.MeaningCode} — {r.MeaningTextAtSign}",
                r.Module,
                $"{r.SubjectDisplay} ({r.SubjectType}/{r.SubjectId})",
                r.LegalEntityId?.ToString("D") ?? "",
                row.VerificationStatus
            }.Select(Cell)).Append("\r\n");
        }

        var preamble = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(builder.ToString());
        return [.. preamble, .. body];
    }

    /// <summary>Quotes every cell; a leading = + - @ (or tab / CR) is prefixed with ' so a spreadsheet never evaluates it.</summary>
    internal static string Cell(string? value)
    {
        var text = value ?? string.Empty;
        if (text.Length > 0 && "=+-@\t\r".Contains(text[0]))
        {
            text = "'" + text;
        }

        return "\"" + text.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}

/// <summary>
/// §4.25 / §4.26 — export (KS-6). Every export (PDF and CSV) writes one insert-only <c>esign_exports</c> row with the file's
/// SHA-256 and an <c>esign.export.created</c> audit event in the same transaction. Visibility is the search's (§4.24).
/// </summary>
public sealed class ExportHandlers :
    IRequestHandler<CreateExportCommand, Response<ESignatureExportFile>>,
    IRequestHandler<ListExportsQuery, Response<PageDto<ExportLogEntryDto>>>
{
    public const int MaxRows = 10_000;

    private readonly IESignActorAccessor _actors;
    private readonly IESignatureVisibility _visibility;
    private readonly IESignatureRecordRepository _records;
    private readonly IESignatureInvalidationRepository _invalidations;
    private readonly IESignatureVerifier _verifier;
    private readonly IESignatureExportRepository _exports;
    private readonly IESignNumberGenerator _numbers;
    private readonly IESignaturePdfRenderer _pdf;
    private readonly IContentStorageGateway _storage;
    private readonly IPlatformTransactionExecutor _transactions;
    private readonly ITransactionalAuditOutboxWriter _audit;

    public ExportHandlers(IESignActorAccessor actors, IESignatureVisibility visibility, IESignatureRecordRepository records,
        IESignatureInvalidationRepository invalidations, IESignatureVerifier verifier, IESignatureExportRepository exports,
        IESignNumberGenerator numbers, IESignaturePdfRenderer pdf, IContentStorageGateway storage, IPlatformTransactionExecutor transactions,
        ITransactionalAuditOutboxWriter audit)
    {
        _actors = actors;
        _visibility = visibility;
        _records = records;
        _invalidations = invalidations;
        _verifier = verifier;
        _exports = exports;
        _numbers = numbers;
        _pdf = pdf;
        _storage = storage;
        _transactions = transactions;
        _audit = audit;
    }

    public async Task<Response<ESignatureExportFile>> Handle(CreateExportCommand request, CancellationToken ct)
    {
        var actor = _actors.Current();
        if (actor is null)
        {
            return ESignResponses.Actor<ESignatureExportFile>();
        }

        if (!ESignResponses.IdempotencyKeyOk(request.IdempotencyKey))
        {
            return ESignResponses.Validation<ESignatureExportFile>("idempotencyKey is required (8–100 characters).");
        }

        if (!ESignatureWire.TryParse<ESignatureExportFormat>(request.Format, out var format))
        {
            return ESignResponses.Validation<ESignatureExportFile>("format must be pdf or csv.");
        }

        if (request.FromUtc is not { } from || request.ToUtc is not { } to || from > to)
        {
            return ESignResponses.Validation<ESignatureExportFile>("filter.fromUtc and filter.toUtc are required and fromUtc must not be after toUtc.");
        }

        var search = new ESignatureSearch(from, to, request.Module, request.SignerUserId, request.MeaningCode, request.LegalEntityId,
            await _visibility.VisibleLegalEntitiesAsync(actor, ct), actor.UserIdText);
        var (records, total) = await _records.SearchAsync(actor.TenantId, search, 0, MaxRows + 1, ct);
        if (total > MaxRows)
        {
            return ESignResponses.Fail<ESignatureExportFile>(ESignCodes.ExportTooLarge, 413, $"The filter matches {total} signatures; at most {MaxRows} per export.");
        }

        var invalidations = await _invalidations.GetBySignaturesAsync(actor.TenantId, records.Select(r => r.Id).ToList(), ct);
        var rows = new List<ESignatureExportRow>(records.Count);
        foreach (var record in records.OrderBy(r => r.SignedAtUtc).ThenBy(r => r.SignatureNumber))
        {
            var verification = await _verifier.VerifyAsync(record, invalidations.GetValueOrDefault(record.Id), ct);
            var png = format == ESignatureExportFormat.Pdf ? await ReadPngAsync(record, ct) : null;
            rows.Add(new ESignatureExportRow(record, verification.Status, png));
        }

        var now = DateTime.UtcNow;
        var prior = await _exports.GetByIdempotencyKeyAsync(actor.TenantId, actor.UserIdText, request.IdempotencyKey!, ct);
        var exportNumber = prior?.ExportNumber ?? $"EXP-{now.Year}-{await _numbers.NextAsync(actor.TenantId, "EXP", now.Year, ct):D4}";
        var filter = new ExportFilterDto(from, to, request.Module, request.SignerUserId, request.MeaningCode, request.LegalEntityId);
        var filterText = FilterText(filter);
        var content = format == ESignatureExportFormat.Pdf
            ? _pdf.Render(new ESignatureExportDocument(exportNumber, filterText, actor.Name, now, rows))
            : ESignatureCsv.Render(rows);
        var fileHash = ESignatureDigest.ImageHash(content);

        var export = new ESignatureExport
        {
            TenantId = actor.TenantId,
            CreatedBy = actor.UserIdText,
            ExportNumber = exportNumber,
            Format = format,
            FilterJson = JsonSerializer.Serialize(filter),
            FilterText = filterText,
            RecordCount = rows.Count,
            ByUserId = actor.UserIdText,
            ByName = actor.Name,
            AtUtc = now,
            FileHash = fileHash,
            IdempotencyKey = request.IdempotencyKey!
        };

        // Every export is logged and audited — an idempotency key only stops a second LOG ROW for the same key (unique
        // index); the export itself, a data leaving the system, is still audited with replay = true.
        try
        {
            await _transactions.ExecuteAsync(async (session, txCt) =>
            {
                var inserted = prior is null;
                if (inserted)
                {
                    await _exports.InsertAsync(session, export, txCt);
                }

                await ESignatureAuditIntent.EnqueueAsync(_audit, session, actor.TenantId, ESignAuditEvents.ExportCreated, AuditOperation.Export,
                    "ESignatureExport", export.Id, AuditActorType.TenantUser, actor.UserId,
                    new Dictionary<string, object?>
                    {
                        ["exportNumber"] = export.ExportNumber,
                        ["format"] = ESignatureWire.Name(format),
                        ["recordCount"] = export.RecordCount,
                        ["fileHash"] = export.FileHash,
                        ["filter"] = filterText,
                        ["replay"] = !inserted
                    }, txCt);
                return inserted;
            }, ct);
        }
        catch (ESignatureDuplicateException)
        {
            return ESignResponses.Fail<ESignatureExportFile>(ESignCodes.ValidationFailed, 409, "A parallel export with the same idempotency key won; retry.");
        }
        catch (PlatformTransactionUnavailableException)
        {
            return ESignResponses.Fail<ESignatureExportFile>(ESignCodes.ReauthUnavailable, 503, "The export could not be logged now; nothing was exported.");
        }

        // A replay answers with the logged entry of the first export; its own file hash is in the replay's audit event.
        var entry = prior is null ? ToDto(export, filter) : ToDto(prior, JsonSerializer.Deserialize<ExportFilterDto>(prior.FilterJson)!);
        var (mediaType, extension) = format == ESignatureExportFormat.Pdf ? ("application/pdf", "pdf") : ("text/csv; charset=utf-8", "csv");
        return Response<ESignatureExportFile>.Success(new ESignatureExportFile(content, mediaType, $"{exportNumber}.{extension}", entry));
    }

    public async Task<Response<PageDto<ExportLogEntryDto>>> Handle(ListExportsQuery request, CancellationToken ct)
    {
        var actor = _actors.Current();
        if (actor is null)
        {
            return ESignResponses.Actor<PageDto<ExportLogEntryDto>>();
        }

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize <= 0 ? 25 : request.PageSize, 1, 100);
        var (items, total) = await _exports.ListAsync(actor.TenantId, (page - 1) * pageSize, pageSize, ct);
        return Response<PageDto<ExportLogEntryDto>>.Success(new PageDto<ExportLogEntryDto>(
            items.Select(e => ToDto(e, JsonSerializer.Deserialize<ExportFilterDto>(e.FilterJson)!)).ToList(), total, page, pageSize));
    }

    private static ExportLogEntryDto ToDto(ESignatureExport e, ExportFilterDto filter) => new(
        e.Id, e.ExportNumber, ESignatureWire.Name(e.Format), e.RecordCount, filter, e.FilterText, new ExportByDto(e.ByUserId, e.ByName), e.AtUtc, e.FileHash);

    private static string FilterText(ExportFilterDto f) => string.Join(" · ", new[]
    {
        $"{f.FromUtc:yyyy-MM-dd}–{f.ToUtc:yyyy-MM-dd} UTC",
        f.Module ?? "all modules",
        f.LegalEntityId?.ToString("D") ?? "all companies",
        f.SignerUserId is null ? null : "signer " + f.SignerUserId,
        f.MeaningCode is null ? null : "meaning " + f.MeaningCode
    }.Where(s => s is not null));

    private async Task<byte[]?> ReadPngAsync(ESignatureRecord record, CancellationToken ct)
    {
        if (record.AppearanceStorageProvider is null || record.AppearanceStoragePath is null)
        {
            return null;
        }

        var opened = await _storage.OpenReadAsync(record.AppearanceStorageProvider, record.AppearanceStoragePath, ct);
        if (!opened.IsSuccessful || opened.Data is null)
        {
            return null;
        }

        await using var stream = opened.Data.Content;
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        // Only an image that still matches its recorded hash is printed; anything else prints as "no visual signature".
        return ESignatureDigest.ImageHash(bytes) == record.AppearanceImageHash ? bytes : null;
    }
}
