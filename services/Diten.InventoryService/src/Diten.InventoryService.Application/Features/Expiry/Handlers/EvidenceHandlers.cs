using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Common.Upstream;
using Diten.InventoryService.Application.Features.Expiry.Commands;
using Diten.InventoryService.Application.Features.Expiry.Ports;
using Diten.InventoryService.Application.Features.Expiry.Queries;
using Diten.InventoryService.Application.Features.Expiry.Services;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Expiry.Entities;
using Diten.InventoryService.Domain.Expiry.Enums;
using Diten.InventoryService.Domain.Expiry.Repositories;
using Diten.InventoryService.Domain.Expiry.Rules;
using Diten.InventoryService.Domain.Repositories;
using Diten.InventoryService.Domain.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.Expiry.Handlers;

/// <summary>
/// WP-0176-F5 Kanıt Paketi period (pack §4.7): a preset — <c>q</c> (first day of the current quarter → today), <c>d30</c> (the last 30 days,
/// today included), <c>ytd</c> (1 January → today) — or <c>custom</c> / none with both <c>from</c> and <c>to</c> (yyyy-MM-dd). A preset
/// takes no dates; <c>to</c> ≥ <c>from</c>, never after today; at most 366 days, both ends inclusive. UTC calendar days.
/// </summary>
public static class ExpiryEvidencePeriod
{
    public sealed record Resolved(string Preset, DateOnly From, DateOnly To)
    {
        public DateTimeOffset FromUtc => new(From.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        /// <summary>Midnight after <see cref="To"/> — the exclusive end of the reads.</summary>
        public DateTimeOffset ToExclusiveUtc => new(To.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
    }

    public static (Resolved? Period, string? Error) Resolve(string? preset, string? from, string? to, DateOnly today)
    {
        DateOnly? f = null;
        DateOnly? t = null;
        if (from is not null)
        {
            if (!TryDay(from, out var day)) return (null, "from must be a date (yyyy-MM-dd).");
            f = day;
        }

        if (to is not null)
        {
            if (!TryDay(to, out var day)) return (null, "to must be a date (yyyy-MM-dd).");
            t = day;
        }

        switch (preset)
        {
            case null or "custom":
                if (f is null || t is null) return (null, "Give a preset (q, d30, ytd) or both from and to.");
                break;
            case "q" or "d30" or "ytd":
                if (f is not null || t is not null) return (null, "A preset takes no from / to.");
                t = today;
                f = preset switch
                {
                    "q" => new DateOnly(today.Year, (today.Month - 1) / 3 * 3 + 1, 1),
                    "d30" => today.AddDays(-29),
                    _ => new DateOnly(today.Year, 1, 1)
                };
                break;
            default:
                return (null, "preset must be q, d30, ytd or custom.");
        }

        if (t!.Value < f!.Value) return (null, "to must not be before from.");
        if (t.Value > today) return (null, "to must not be after today.");
        if (t.Value.DayNumber - f.Value.DayNumber + 1 > ExpiryContract.MaxEvidenceDays)
            return (null, $"The period covers at most {ExpiryContract.MaxEvidenceDays} days.");
        return (new Resolved(preset ?? "custom", f.Value, t.Value), null);
    }

    /// <summary>The warehouse ids (canonical UUIDs) and exception types (wire codes) of the scope — distinct, in a stable order.</summary>
    public static (IReadOnlyList<string> WarehouseIds, IReadOnlyList<ExpiryExceptionType> Types, string? Error) Scope(
        IReadOnlyList<string> warehouseIds, IReadOnlyList<string> types)
    {
        var warehouses = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var raw in warehouseIds)
        {
            if (!ExpirySupport.TryParseId(raw, out var id)) return ([], [], "warehouseIds must be canonical UUIDs.");
            warehouses.Add(id.ToString("D"));
        }

        var parsed = new SortedSet<ExpiryExceptionType>();
        foreach (var raw in types)
        {
            if (!ExpiryWire.TryParseType(raw, out var type)) return ([], [], "types must be exception type codes.");
            parsed.Add(type);
        }

        return (warehouses.ToList(), parsed.ToList(), null);
    }

    public static string Day(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static bool TryDay(string raw, out DateOnly day)
        => DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day);
}

/// <summary>
/// The four sections of the Kanıt Paketi, read ONCE and given both to the preview and to the file renderers (so the file shows exactly
/// what the preview showed): (1) the summary KPIs — the pack §8.8 formulas (<see cref="ExpiryKpiFormulas"/>), FEFO issues and resolutions
/// of the period, stock and active cases as of now, for the warehouse set; (2) the cases OPENED in the period (types filter), with their
/// resolution, reason and MOD-0022 signature id; (3) the policy changes in the period; (4) the FEFO_BYPASS cases of the period (the types
/// filter does not apply to this section). Company scoped through the repository and the read model.
/// </summary>
public sealed class ExpiryEvidenceBuilder
{
    private readonly IExpiryRepository _repository;
    private readonly IExpiryReadModel _reads;
    private readonly ExpiryLotView _view;
    private readonly ExpiryReads _cases;
    private readonly ExpirySupport _support;
    private readonly IExpiryESignClient _esign;
    private readonly IExpiryAuditNames _auditNames;
    private readonly IProductMasterClient _products;
    private readonly ILegalEntityMasterClient _companies;
    private readonly ILocationRepository _locations;

    public ExpiryEvidenceBuilder(IExpiryRepository repository, IExpiryReadModel reads, ExpiryLotView view, ExpiryReads cases, ExpirySupport support,
        IExpiryESignClient esign, IExpiryAuditNames auditNames, IProductMasterClient products, ILegalEntityMasterClient companies, ILocationRepository locations)
    {
        _repository = repository;
        _reads = reads;
        _view = view;
        _cases = cases;
        _support = support;
        _esign = esign;
        _auditNames = auditNames;
        _products = products;
        _companies = companies;
        _locations = locations;
    }

    /// <summary>
    /// The preview and the file document. WP-MVP1-FIX-LIVE2 (FU-0176-EV-NAMES): the document carries names beside the ids — company (MDM),
    /// product / SKU (PRODUCT-MASTER), warehouses (MOD-0173 master), the signer of each resolution (MOD-0022 <c>signerNameAtSign</c>) and the
    /// user of each policy change (the central audit queue's name for that change's correlation id and user) — read now with the caller's
    /// token; an unreadable one is the id + "(okunamadı)". <paramref name="forPdf"/>: worded in a language the PDF font draws (zh / ar → en).
    /// The preview (wire) is unchanged.
    /// </summary>
    public async Task<(ExpiryEvidencePreviewDto Preview, ExpiryEvidenceDocument Document)> BuildAsync(
        ExpiryEvidencePeriod.Resolved period, IReadOnlyList<string> warehouseIds, IReadOnlyList<ExpiryExceptionType> types, string language,
        CancellationToken ct, bool forPdf = false)
    {
        var requested = ExpiryEvidenceText.Normal(language);
        language = forPdf ? ExpiryEvidenceText.PdfLanguage(requested) : requested;
        var now = _support.Now();
        var context = await _view.LoadAsync(null, null, ct);
        var rows = warehouseIds.Count == 0 ? context.Rows : context.Rows.Where(r => warehouseIds.Contains(r.WarehouseId, StringComparer.Ordinal)).ToList();
        var dated = rows.Select(r => (Row: r, Days: ExpiryLotView.Days(r, context.Today))).ToList();
        var figures = await _reads.EvidenceFiguresAsync(now, period.FromUtc, period.ToExclusiveUtc, warehouseIds, ct);
        var summary = ExpiryKpiFormulas.Kpis(ExpiryKpiFormulas.Stock(dated, context.Policy), figures.Issues, figures.Compliant, figures.Open,
            figures.Late, figures.AvgResolutionHours);

        var cases = await _repository.CasesOpenedBetweenAsync(period.FromUtc, period.ToExclusiveUtc, warehouseIds, [], ct);
        var warehouses = await _cases.WarehousesAsync(cases, ct);
        var listed = types.Count == 0 ? cases : cases.Where(c => types.Contains(c.Type)).ToList();
        var bypasses = cases.Where(c => c.Type == ExpiryExceptionType.FefoBypass).ToList();
        var changes = await _repository.PolicyHistoryBetweenAsync(period.FromUtc, period.ToExclusiveUtc, ct);
        var history = changes.Select(ExpiryMapping.History).ToList();

        // ── names (read now; unreadable → id + "(okunamadı)") ──
        var audit = await _auditNames.NamesAsync(changes.Select(h => (h.CorrelationId, h.ChangedBy)).Distinct().ToList(), ct);
        var changedBy = changes.Select(h => audit.TryGetValue((h.CorrelationId, h.ChangedBy), out var n) ? n
            : ExpiryEvidenceText.Format("ExpiryPdf.NameUnreadable", language, h.ChangedBy.ToString("D"))).ToList();
        var names = new Names(language);
        foreach (var c in listed.Concat(bypasses).DistinctBy(c => c.Id))
        {
            if (!names.Items.ContainsKey(c.ItemId)) names.Items[c.ItemId] = await ItemTextAsync(c.ItemId, language, ct);
            if (!names.Skus.ContainsKey((c.SkuId, c.SkuLevel))) names.Skus[(c.SkuId, c.SkuLevel)] = await SkuCodeAsync(c.SkuId, c.SkuLevel, language, ct);
            if (c.ResolutionSignatureId is { } sid && !names.Signers.ContainsKey(sid)) names.Signers[sid] = await SignerAsync(sid, ct);
        }

        var scopeWarehouses = warehouseIds.Count == 0 ? []
            : (await _locations.FindManyAsync(warehouseIds.Select(Guid.Parse).ToList(), ct)).ToDictionary(l => l.Id.ToString("D"), StringComparer.OrdinalIgnoreCase);
        var warehouseTexts = warehouseIds.Select(id => scopeWarehouses.TryGetValue(id, out var l) ? Place(l)
            : ExpiryEvidenceText.Format("ExpiryPdf.Unreadable", language, id)).ToList();

        var typeCodes = types.Select(ExpiryWire.Code).ToList();
        var preview = new ExpiryEvidencePreviewDto(
            new ExpiryEvidencePeriodDto(period.Preset, ExpiryEvidencePeriod.Day(period.From), ExpiryEvidencePeriod.Day(period.To)),
            summary,
            listed.Select(c => Ref(c, warehouses, now)).ToList(),
            history,
            bypasses.Select(c => Ref(c, warehouses, now)).ToList());
        var document = new ExpiryEvidenceDocument(
            _support.LegalEntityId.ToString("D"), period.Preset, period.From, period.To, warehouseIds, typeCodes, now, language, summary,
            listed.Select(c => Row(c, warehouses, names)).ToList(), history, bypasses.Select(c => Row(c, warehouses, names)).ToList(),
            await CompanyTextAsync(_support.LegalEntityId, language, ct), warehouseTexts, changedBy, requested);
        return (preview, document);
    }

    /// <summary>The names read for one document (per distinct product, SKU, signature).</summary>
    private sealed class Names(string language)
    {
        public string Language { get; } = language;
        public Dictionary<Guid, string> Items { get; } = [];
        public Dictionary<(Guid, SkuLevel), string> Skus { get; } = [];
        public Dictionary<string, (string? UserId, string? Name)> Signers { get; } = new(StringComparer.Ordinal);
    }

    private async Task<string> CompanyTextAsync(Guid legalEntityId, string language, CancellationToken ct)
    {
        try
        {
            if (await _companies.GetLegalEntityNameAsync(legalEntityId, ct) is { } name) return $"{name.Code} · {name.Name}";
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // fall through: unreadable
        }

        return ExpiryEvidenceText.Format("ExpiryPdf.Unreadable", language, legalEntityId.ToString("D"));
    }

    private async Task<string> ItemTextAsync(Guid itemId, string language, CancellationToken ct)
    {
        try
        {
            if (await _products.GetProductAsync(itemId, ct) is { } p && (!string.IsNullOrWhiteSpace(p.CanonicalCode) || !string.IsNullOrWhiteSpace(p.Name)))
                return string.Join(" · ", new[] { p.CanonicalCode, p.Name }.Where(v => !string.IsNullOrWhiteSpace(v)));
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // fall through: unreadable
        }

        return ExpiryEvidenceText.Format("ExpiryPdf.Unreadable", language, itemId.ToString("D"));
    }

    private async Task<string> SkuCodeAsync(Guid skuId, SkuLevel level, string language, CancellationToken ct)
    {
        try
        {
            if (await _products.GetSkuAsync(skuId, level, ct) is { CanonicalCode: { Length: > 0 } code }) return code;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // fall through: unreadable
        }

        return ExpiryEvidenceText.Format("ExpiryPdf.Unreadable", language, skuId.ToString("D"));
    }

    /// <summary>The signer of a resolution as MOD-0022 recorded it (<c>signerNameAtSign</c>); unreadable → (null, null).</summary>
    private async Task<(string? UserId, string? Name)> SignerAsync(string signatureId, CancellationToken ct)
    {
        var read = await _esign.SignatureAsync(signatureId, ct);
        return read.IsOk ? (read.Value!.SignerUserId, string.IsNullOrWhiteSpace(read.Value.SignerNameAtSign) ? null : read.Value.SignerNameAtSign) : (null, null);
    }

    private static string Place(Location l) => string.Join(" · ", new[] { l.Code, l.Name }.Where(v => !string.IsNullOrWhiteSpace(v)).Distinct(StringComparer.Ordinal));

    private static ExpiryExceptionRefDto Ref(FefoExceptionCase c, IReadOnlyDictionary<Guid, Location> warehouses, DateTimeOffset now)
        => ExpiryMapping.Ref(c, warehouses, now) with
        {
            ResolutionReason = c.ResolutionReason,
            ResolutionSignatureId = c.ResolutionSignatureId,
            ResolvedAtUtc = ExpirySupport.Wire(c.ResolvedAtUtc)
        };

    private static ExpiryEvidenceCaseRow Row(FefoExceptionCase c, IReadOnlyDictionary<Guid, Location> warehouses, Names names)
    {
        var (signerId, signerName) = c.ResolutionSignatureId is { } sid && names.Signers.TryGetValue(sid, out var s) ? s : (null, null);
        // the signer's name at signing; unreadable → the signer id (or, when MOD-0022 did not answer, the signature id) + "(ad okunamadı)"
        var unread = signerId ?? c.ResolutionSignatureId;
        var signer = signerName ?? (unread is null ? null : ExpiryEvidenceText.Format("ExpiryPdf.NameUnreadable", names.Language, unread));
        var warehouseName = c.WarehouseId is null ? null
            : Guid.TryParse(c.WarehouseId, out var wid) && warehouses.TryGetValue(wid, out var w) ? Place(w)
            : ExpiryEvidenceText.Format("ExpiryPdf.Unreadable", names.Language, c.WarehouseId);
        return Row(c, warehouses) with
        {
            ItemText = names.Items.GetValueOrDefault(c.ItemId),
            SkuCode = names.Skus.GetValueOrDefault((c.SkuId, c.SkuLevel)),
            WarehouseName = warehouseName,
            SignerName = signer,
            SignerUserId = signerId
        };
    }

    private static ExpiryEvidenceCaseRow Row(FefoExceptionCase c, IReadOnlyDictionary<Guid, Location> warehouses) => new(
        c.Number,
        ExpiryWire.Code(c.Type),
        c.ItemId.ToString("D"),
        WireNames.ToWire(c.SkuLevel),
        c.LotNumber,
        c.WarehouseId is null ? null
            : Guid.TryParse(c.WarehouseId, out var id) && warehouses.TryGetValue(id, out var location) && !string.IsNullOrWhiteSpace(location.Code) ? location.Code
            : c.WarehouseId,
        ExpirySupport.Wire(c.OpenedAtUtc),
        c.Status.ToString(),
        c.Resolution?.ToString(),
        c.ResolutionReason,
        c.ResolutionSignatureId,
        c.Facts.GetValueOrDefault("firstEligibleLotNumber"),
        c.Facts.GetValueOrDefault("bypassReason"));
}

/// <summary>
/// The CSV of the Kanıt Paketi (FEFO-BUNDLE createEvidenceExport): <c>istisna_no;tur;urun;lot;depo;acilis;durum;cozum;imza</c>, one line
/// per case of the period, UTF-8 with BOM, CRLF, <c>;</c> separator, RFC 4180 quoting; a cell that starts with = + - @ is prefixed with
/// <c>'</c> (spreadsheet formula injection).
/// <para>WP-MVP1-FIX-LIVE2 (additive): the nine columns stay where they were (codes and ids); the names and the worded values follow them —
/// <c>urun_adi;sku_kodu;depo_adi;tur_adi;durum_adi;cozum_adi;imzalayan;imzalayan_kimlik</c>, in the document's language.</para>
/// </summary>
public static class ExpiryEvidenceCsv
{
    public const string Header = "istisna_no;tur;urun;lot;depo;acilis;durum;cozum;imza";

    /// <summary>The columns appended by WP-MVP1-FIX-LIVE2 (names / worded values beside the codes).</summary>
    public const string NameColumns = "urun_adi;sku_kodu;depo_adi;tur_adi;durum_adi;cozum_adi;imzalayan;imzalayan_kimlik";

    public static byte[] Render(ExpiryEvidenceDocument document)
    {
        var l = document.Language;
        var text = new StringBuilder().Append(Header).Append(';').Append(NameColumns).Append("\r\n");
        foreach (var r in document.Exceptions)
        {
            text.Append(string.Join(';', new[]
                {
                    r.Number, r.Type, r.ItemId, r.LotNumber, r.Warehouse, r.OpenedAtUtc, r.Status, r.Resolution, r.SignatureId,
                    r.ItemText, r.SkuCode, r.WarehouseName, ExpiryEvidenceText.Value("Expiry.Type.", r.Type, l), ExpiryEvidenceText.Value("Expiry.Status.", r.Status, l),
                    r.Resolution is null ? null : ExpiryEvidenceText.Value("Expiry.Resolution.", r.Resolution, l), r.SignerName, r.SignerUserId
                }.Select(Cell))).Append("\r\n");
        }

        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(text.ToString())];
    }

    private static string Cell(string? value)
    {
        var v = value ?? string.Empty;
        if (v.Length > 0 && v[0] is '=' or '+' or '-' or '@') v = "'" + v;
        return v.IndexOfAny([';', '"', '\r', '\n']) >= 0 ? "\"" + v.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"" : v;
    }
}

/// <summary>previewEvidence — the four sections for the period and scope, nothing stored.</summary>
public sealed class PreviewExpiryEvidenceHandler : IRequestHandler<PreviewExpiryEvidenceQuery, Response<ExpiryEvidencePreviewDto>>
{
    private readonly ExpiryEvidenceBuilder _builder;
    private readonly ExpirySupport _support;

    public PreviewExpiryEvidenceHandler(ExpiryEvidenceBuilder builder, ExpirySupport support)
    {
        _builder = builder;
        _support = support;
    }

    public async Task<Response<ExpiryEvidencePreviewDto>> Handle(PreviewExpiryEvidenceQuery request, CancellationToken ct)
    {
        var (warehouses, types, scopeError) = ExpiryEvidencePeriod.Scope(request.WarehouseIds, request.Types);
        if (scopeError is not null) return ExpirySupport.Invalid<ExpiryEvidencePreviewDto>(scopeError);
        var (period, error) = ExpiryEvidencePeriod.Resolve(request.Preset, request.From, request.To, ExpiryLotView.Today(_support.Now()));
        if (period is null) return ExpirySupport.Invalid<ExpiryEvidencePreviewDto>(error!);
        var (preview, _) = await _builder.BuildAsync(period, warehouses, types, "en", ct);
        return Response<ExpiryEvidencePreviewDto>.Success(preview);
    }
}

/// <summary>
/// createEvidenceExport — CSV or PDF of the period; <c>sha256</c> = SHA-256 OF THE PRODUCED FILE BYTES (pack §4.7, §21.3 D-8 — K3 sabotage
/// (b): hash the parameters → the independent-hash test is red). The export-log row, the bytes and the audit row
/// <c>expiry.evidence.exported</c> are one transaction; the same key + the same body answers the stored export (200, the SAME file and hash),
/// another body 400 <c>IDEMPOTENCY_KEY_REUSED</c>.
/// </summary>
public sealed class CreateExpiryEvidenceExportHandler : IRequestHandler<CreateExpiryEvidenceExportCommand, Response<ExpiryEvidenceExportDto>>
{
    private readonly IExpiryRepository _repository;
    private readonly ExpiryEvidenceBuilder _builder;
    private readonly IExpiryEvidencePdfRenderer _pdf;
    private readonly ExpirySupport _support;

    public CreateExpiryEvidenceExportHandler(IExpiryRepository repository, ExpiryEvidenceBuilder builder, IExpiryEvidencePdfRenderer pdf, ExpirySupport support)
    {
        _repository = repository;
        _builder = builder;
        _pdf = pdf;
        _support = support;
    }

    public async Task<Response<ExpiryEvidenceExportDto>> Handle(CreateExpiryEvidenceExportCommand request, CancellationToken ct)
    {
        if (request.Format is not ("Csv" or "Pdf")) return ExpirySupport.Invalid<ExpiryEvidenceExportDto>("format must be Csv or Pdf.");
        var format = request.Format == "Csv" ? ExpiryEvidenceFormat.Csv : ExpiryEvidenceFormat.Pdf;
        var (warehouses, types, scopeError) = ExpiryEvidencePeriod.Scope(request.WarehouseIds, request.Types);
        if (scopeError is not null) return ExpirySupport.Invalid<ExpiryEvidenceExportDto>(scopeError);
        var fingerprint = ExpirySupport.Fingerprint(("preset", request.Preset), ("from", request.From), ("to", request.To),
            ("warehouseIds", string.Join(',', warehouses)), ("types", string.Join(',', types.Select(ExpiryWire.Code))), ("format", request.Format));
        if (await _repository.FindEvidenceExportByKeyAsync(_support.UserId, request.IdempotencyKey, ct) is { } earlier)
        {
            return earlier.RequestFingerprint == fingerprint
                ? Response<ExpiryEvidenceExportDto>.Success(Map(earlier))
                : ExpirySupport.KeyReused<ExpiryEvidenceExportDto>();
        }

        var now = _support.Now();
        var (period, error) = ExpiryEvidencePeriod.Resolve(request.Preset, request.From, request.To, ExpiryLotView.Today(now));
        if (period is null) return ExpirySupport.Invalid<ExpiryEvidenceExportDto>(error!);

        // WP-MVP1-FIX-LIVE2: a PDF is worded in a language its font draws (zh / ar → en + note); the CSV in the selected one
        var (_, document) = await _builder.BuildAsync(period, warehouses, types, request.Language, ct, forPdf: format == ExpiryEvidenceFormat.Pdf);
        var bytes = format == ExpiryEvidenceFormat.Csv ? ExpiryEvidenceCsv.Render(document) : _pdf.Render(document);
        var id = Guid.NewGuid();
        var export = new ExpiryEvidenceExport
        {
            Id = id,
            CreatedAt = now,
            Preset = period.Preset,
            PeriodFrom = period.FromUtc,
            PeriodTo = new DateTimeOffset(period.To.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
            WarehouseIds = warehouses.ToList(),
            Types = types.ToList(),
            Format = format,
            FileName = $"expiry-evidence-{period.From:yyyyMMdd}-{period.To:yyyyMMdd}-{id.ToString("N")[..8]}.{(format == ExpiryEvidenceFormat.Csv ? "csv" : "pdf")}",
            ContentType = format == ExpiryEvidenceFormat.Csv ? "text/csv; charset=utf-8" : "application/pdf",
            Sha256 = Sha256Hex(bytes),
            SizeBytes = bytes.LongLength,
            RowCount = document.Exceptions.Count,
            ExportedBy = _support.UserId,
            ExportedAtUtc = now,
            IdempotencyKey = request.IdempotencyKey,
            RequestFingerprint = fingerprint,
            Version = 1
        };
        var file = new ExpiryEvidenceFile { Id = id, CreatedAt = now, Content = bytes, Version = 1 };
        try
        {
            await _repository.InTransactionAsync<bool>(async (session, tx) =>
            {
                await session.InsertEvidenceExportAsync(export, file, tx);
                await session.InsertAuditAsync(_support.Audit(ExpiryContract.AuditEvidenceExported, ExpiryContract.SubjectEvidenceExport, id, null, now,
                    request.IdempotencyKey, new Dictionary<string, string?>
                    {
                        ["format"] = request.Format,
                        ["fileName"] = export.FileName,
                        ["sha256"] = export.Sha256,
                        ["rowCount"] = export.RowCount.ToString(CultureInfo.InvariantCulture),
                        ["preset"] = export.Preset,
                        ["from"] = ExpiryEvidencePeriod.Day(period.From),
                        ["to"] = ExpiryEvidencePeriod.Day(period.To),
                        ["warehouseIds"] = string.Join(',', warehouses),
                        ["types"] = string.Join(',', document.Types)
                    }), tx);
                return true;
            }, ct);
        }
        catch (ExpiryDuplicateException)
        {
            return await _repository.FindEvidenceExportByKeyAsync(_support.UserId, request.IdempotencyKey, ct) is { } twin && twin.RequestFingerprint == fingerprint
                ? Response<ExpiryEvidenceExportDto>.Success(Map(twin))
                : ExpirySupport.KeyReused<ExpiryEvidenceExportDto>();
        }

        return Response<ExpiryEvidenceExportDto>.Success(Map(export), 201);
    }

    /// <summary>Lower-case hex SHA-256 of the file bytes.</summary>
    public static string Sha256Hex(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    public static ExpiryEvidenceExportDto Map(ExpiryEvidenceExport e) => new(
        e.Id.ToString("D"),
        new ExpiryEvidencePeriodDto(e.Preset, ExpirySupport.WireDate(e.PeriodFrom)!, ExpirySupport.WireDate(e.PeriodTo)!),
        new ExpiryEvidenceScopeDto(e.WarehouseIds.Count == 0 ? null : e.WarehouseIds, e.Types.Count == 0 ? null : e.Types.Select(ExpiryWire.Code).ToList()),
        e.Format.ToString(),
        e.FileName,
        e.Sha256,
        e.RowCount,
        ExpiryMapping.User(e.ExportedBy),
        ExpirySupport.Wire(e.ExportedAtUtc));
}

/// <summary>listEvidenceExports — the company's export log, newest first (paging 1–200).</summary>
public sealed class ListExpiryEvidenceExportsHandler : IRequestHandler<ListExpiryEvidenceExportsQuery, Response<ExpiryEvidenceExportPageDto>>
{
    private readonly IExpiryRepository _repository;

    public ListExpiryEvidenceExportsHandler(IExpiryRepository repository) => _repository = repository;

    public async Task<Response<ExpiryEvidenceExportPageDto>> Handle(ListExpiryEvidenceExportsQuery request, CancellationToken ct)
    {
        if (ExpiryPaging.Parse(request.Page, request.PageSize) is not { } paging)
            return ExpirySupport.Invalid<ExpiryEvidenceExportPageDto>($"page must be ≥ 1 and pageSize 1-{ExpiryContract.MaxPageSize}.");
        var result = await _repository.ListEvidenceExportsAsync(paging.Page, paging.PageSize, ct);
        return Response<ExpiryEvidenceExportPageDto>.Success(new ExpiryEvidenceExportPageDto(
            result.Items.Select(CreateExpiryEvidenceExportHandler.Map).ToList(), result.Total, paging.Page, paging.PageSize));
    }
}

/// <summary>getEvidenceExportFile — the STORED bytes of the export (what was hashed); unknown → 404 (another company → the controller answers 403).</summary>
public sealed class GetExpiryEvidenceFileHandler : IRequestHandler<GetExpiryEvidenceFileQuery, Response<ExpiryEvidenceFileDto>>
{
    private readonly IExpiryRepository _repository;

    public GetExpiryEvidenceFileHandler(IExpiryRepository repository) => _repository = repository;

    public async Task<Response<ExpiryEvidenceFileDto>> Handle(GetExpiryEvidenceFileQuery request, CancellationToken ct)
    {
        if (!ExpirySupport.TryParseId(request.ExportId, out var id)
            || await _repository.FindEvidenceExportAsync(id, ct) is not { } export
            || await _repository.FindEvidenceFileAsync(id, ct) is not { } file)
        {
            return ExpirySupport.Fail<ExpiryEvidenceFileDto>(ExpiryErrorCodes.EvidenceExportNotFound, "Evidence export not found.", 404);
        }

        return Response<ExpiryEvidenceFileDto>.Success(new ExpiryEvidenceFileDto(file.Content, export.ContentType, export.FileName, export.Sha256));
    }
}
