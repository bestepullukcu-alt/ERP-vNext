using Diten.CrmService.Application.Common;
using Diten.CrmService.Domain.Entities.Commercial;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Diten.CrmService.Application.Features.Commercial.PreOrders;

/// <summary>The two copies of the depot document (MOD-0168 §4.7).</summary>
public static class PreOrderDocumentCopies
{
    public const string Depot = "depot";
    public const string Pharmacy = "pharmacy";

    public static readonly IReadOnlyList<string> All = new[] { Depot, Pharmacy };

    public static bool IsValid(string? copy) => copy is Depot or Pharmacy;
}

public sealed record PreOrderDocumentLine(
    int LineNo, string Product, string? Barcode, string? Form, int Quantity, string? Mf, int FreeQuantity, decimal? DiscountPct,
    decimal? UnitPrice, decimal? VatRate, decimal? NetAmount);

public sealed record PreOrderDocumentTotals(decimal Net, decimal Vat, decimal Gross, decimal? EffectiveDiscountPct);

public sealed record PreOrderDocumentFx(string LocalCurrency, decimal Rate, DateOnly RateDate, string Source, decimal? LocalGross);

/// <summary>
/// WP-VW-W6 W6-BE-c2 — EVERYTHING the depot document prints, for one copy, in one language. The ONLY source of the PDF and
/// of its preview (one template, MOD-0168 §4.7: two copy templates are forbidden). A value the order does not carry (GLN,
/// tax number, pharmacy code, dosage form, the seller's address …) is null and is not printed at all — no label, no "—".
/// The depot copy carries no discount percentage of any kind (DESIGN-GAP §3: internal information).
/// </summary>
public sealed record PreOrderDocumentModel(
    string Copy,
    string Language,
    string OrderCode,
    DateOnly? OrderDate,
    DateOnly? DeliveryDate,
    string SellerName,
    string? DepotName,
    string? RepName,
    string PharmacyName,
    string? PharmacyAddress,
    string? PharmacyGln,
    string? PharmacyCode,
    string? PharmacyTaxNumber,
    bool Priced,
    string? Currency,
    IReadOnlyList<PreOrderDocumentLine> Lines,
    PreOrderDocumentTotals? Totals,
    PreOrderDocumentFx? Fx,
    string? SignerName,
    DateTimeOffset? SignedAt,
    byte[]? SignatureImage,
    bool AutoApproved,
    string? ApprovedBy,
    DateTimeOffset? ApprovedAt,
    string? Note,
    int? TransmissionAttempt)
{
    public bool IsDepotCopy => Copy == PreOrderDocumentCopies.Depot;
}

/// <summary>What the renderer printed: the PDF, its file name and the printed text in reading order (the "text extraction"
/// the tests read — PDFsharp writes glyph ids, not text, into the file).</summary>
public sealed record PreOrderRenderedDocument(byte[] Pdf, string FileName, IReadOnlyList<string> Text);

/// <summary>The one depot document template (PDFsharp-MigraDoc in Infrastructure), for the PDF and the preview alike.</summary>
public interface IPreOrderDocumentRenderer
{
    PreOrderRenderedDocument Render(PreOrderDocumentModel model);
}

/// <summary>The MOD-0262 door of the system step (PLT-W6-DOC, internal key, no user): the depot document in, the
/// pharmacist's signature out. Never throws: null = could not be written / read.</summary>
public interface IPreOrderDocumentStore
{
    Task<Guid?> StoreAsync(Guid tenantId, Guid preOrderId, string fileName, byte[] pdf, CancellationToken ct);

    Task<byte[]?> ReadAsync(Guid tenantId, Guid documentRef, CancellationToken ct);
}

/// <summary><c>Commercial:PreOrders:Document:*</c>.</summary>
public interface IPreOrderDocumentSettings
{
    /// <summary>The document language when the selling company's country speaks none of the seven (the tenant's language
    /// is not readable by CRM without a user; default <c>en</c>, the platform floor).</summary>
    string FallbackLanguage { get; }
}

public static class PreOrderDocumentRules
{
    /// <summary>One document at most the internal dispatch's attachment budget (PLT-W6) and the scope's own cap (PLT-W6-DOC).</summary>
    public const int MaxBytes = 256 * 1024;

    public const string TooLarge = "document_too_large";
    public const string StoreFailed = "document_store_unavailable";
    public const string SignatureUnavailable = "signature_unavailable";

    /// <summary>The document language: the selling company's country language (decision B), else the fallback.</summary>
    public static string LanguageOf(PreOrder order, string fallback)
        => ExternalRecipientLanguages.ForCountry(order.CountryCode) ?? (string.IsNullOrWhiteSpace(fallback) ? "en" : fallback.Trim().ToLowerInvariant());

    public static string FileName(PreOrder order, string copy) => $"{order.Code}-{copy}.pdf";

    /// <summary>
    /// The model of one copy. The depot copy drops every discount percentage (line, combined, effective); the pharmacy copy
    /// keeps the line discount and the effective one. Fields the snapshots do not carry stay null and are not printed.
    /// </summary>
    public static PreOrderDocumentModel Build(PreOrder order, string copy, string language, byte[]? signatureImage, int? attemptNo = null)
    {
        var depot = copy == PreOrderDocumentCopies.Depot;
        var priced = order.PriceMode == PreOrderPriceModes.Priced;
        var approver = order.Approval?.DecidedByUserId is { } by && order.ApprovalRequirement?.ApproverNames.TryGetValue(by.ToString("D"), out var name) == true
            ? name
            : null;
        var submitted = order.Events.LastOrDefault(e => e.Type == PreOrderEventTypes.Submitted)?.At;
        return new PreOrderDocumentModel(
            Copy: copy,
            Language: language,
            OrderCode: order.Code,
            OrderDate: submitted is { } at ? DateOnly.FromDateTime(at.UtcDateTime) : null,
            DeliveryDate: order.RequestedDeliveryDate,
            SellerName: order.Seller.Name,
            DepotName: Text(order.Depot?.Name),
            RepName: Text(order.Rep.DisplayName),
            PharmacyName: order.Pharmacy.Name,
            PharmacyAddress: Text(order.Pharmacy.Address),
            PharmacyGln: Text(order.Pharmacy.Gln),
            PharmacyCode: Text(order.Pharmacy.PharmacyCode),
            PharmacyTaxNumber: Text(order.Pharmacy.TaxNumber),
            Priced: priced,
            Currency: priced ? order.Currency : null,
            Lines: order.Lines.OrderBy(l => l.LineNo).Select(l => new PreOrderDocumentLine(
                l.LineNo, l.Sku.Name, Text(l.Sku.Gtin), Text(l.Sku.Form), l.Quantity, l.Mf is { } mf ? $"{mf.Buy}+{mf.Free}" : null, l.FreeQuantity,
                depot ? null : l.DiscountPct,
                priced ? l.UnitPrice : null, priced ? l.VatRate : null, priced ? l.NetAmount : null)).ToList(),
            Totals: priced && order.Totals is { } t ? new PreOrderDocumentTotals(t.Net, t.Vat, t.Gross, depot ? null : t.EffectiveDiscountPct) : null,
            Fx: priced && order.Fx is { } fx ? new PreOrderDocumentFx(fx.LocalCurrency, fx.Rate, fx.RateDate, fx.Source, fx.LocalGross) : null,
            SignerName: Text(order.Signature?.SignerName),
            SignedAt: order.Signature?.SignedAt,
            SignatureImage: signatureImage,
            AutoApproved: order.Approval?.Auto == true,
            ApprovedBy: order.Approval?.Auto == true ? null : approver,
            ApprovedAt: order.Approval?.DecidedAt,
            Note: Text(order.Note),
            TransmissionAttempt: attemptNo ?? (order.Transmission?.Attempts.Count is > 0 and var n ? n : null));
    }

    private static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// WP-VW-W6 W6-BE-c2 — the depot document of one transmission attempt (W6-BE-c1's port): each copy rendered from the ONE
/// template, checked against 256 KB (over it → the attempt fails with <c>document_too_large</c>, nothing is cut), written to
/// MOD-0262 through the internal door. A copy whose ref is already bound to the order is NOT rendered again (an attempt that
/// failed after the documents were written reuses them — each write would open a new version in MOD-0262).
/// </summary>
public sealed class PreOrderDocumentProducer : IPreOrderDocumentProducer
{
    private readonly IPreOrderDocumentRenderer _renderer;
    private readonly IPreOrderDocumentStore _store;
    private readonly IPreOrderDocumentSettings _settings;
    private readonly ILogger<PreOrderDocumentProducer> _logger;

    public PreOrderDocumentProducer(IPreOrderDocumentRenderer renderer, IPreOrderDocumentStore store, IPreOrderDocumentSettings settings,
        ILogger<PreOrderDocumentProducer>? logger = null)
    {
        _renderer = renderer;
        _store = store;
        _settings = settings;
        _logger = logger ?? NullLogger<PreOrderDocumentProducer>.Instance;
    }

    public async Task<PreOrderDocuments> ProduceAsync(PreOrder order, int attemptNo, CancellationToken ct)
    {
        var depotRef = order.Transmission?.DepotDocumentRef;
        var pharmacyRef = order.Transmission?.PharmacyCopyRef;
        if (depotRef is not null && pharmacyRef is not null)
        {
            return new PreOrderDocuments(true, depotRef, pharmacyRef);
        }

        byte[]? signature = null;
        if (order.Signature?.BinaryRef is { } signatureRef && signatureRef != Guid.Empty)
        {
            signature = await _store.ReadAsync(order.TenantId, signatureRef, ct);
            if (signature is null)
            {
                return new PreOrderDocuments(false, depotRef, pharmacyRef, PreOrderDocumentRules.SignatureUnavailable);
            }
        }

        var language = PreOrderDocumentRules.LanguageOf(order, _settings.FallbackLanguage);
        foreach (var copy in PreOrderDocumentCopies.All)
        {
            if ((copy == PreOrderDocumentCopies.Depot ? depotRef : pharmacyRef) is not null)
            {
                continue;
            }

            var rendered = _renderer.Render(PreOrderDocumentRules.Build(order, copy, language, signature, attemptNo));
            if (rendered.Pdf.Length > PreOrderDocumentRules.MaxBytes)
            {
                _logger.LogWarning("pre_order.document.too_large PreOrderId={Id} Copy={Copy} Bytes={Bytes}", order.Id, copy, rendered.Pdf.Length);
                return new PreOrderDocuments(false, depotRef, pharmacyRef, PreOrderDocumentRules.TooLarge);
            }

            var stored = await _store.StoreAsync(order.TenantId, order.Id, PreOrderDocumentRules.FileName(order, copy), rendered.Pdf, ct);
            if (stored is null)
            {
                return new PreOrderDocuments(false, depotRef, pharmacyRef, PreOrderDocumentRules.StoreFailed);
            }

            if (copy == PreOrderDocumentCopies.Depot)
            {
                depotRef = stored;
            }
            else
            {
                pharmacyRef = stored;
            }
        }

        return new PreOrderDocuments(true, depotRef, pharmacyRef);
    }
}

/// <summary>The rendered file of one copy (preview / download).</summary>
public sealed record PreOrderDocumentFile(byte[] Content, string FileName);

/// <summary>
/// WP-VW-W6 W6-BE-c2 — MOD-0168 §15 <c>GET api/commercial/pre-orders/{id}/document?copy=depot|pharmacy</c>: the copy rendered
/// by THE template (the same renderer, model and language rule the transmission uses — two templates are forbidden), in the
/// W6-BE-a visibility (own / team / approver / read-all; anything else 404). A signature that cannot be read is left out
/// of the preview (it never blocks it).
/// </summary>
public sealed record GetPreOrderDocumentQuery(Guid Id, string? Copy) : MediatR.IRequest<Diten.CrmService.Application.Common.Models.Response<PreOrderDocumentFile>>;

public sealed class GetPreOrderDocumentHandler
    : MediatR.IRequestHandler<GetPreOrderDocumentQuery, Diten.CrmService.Application.Common.Models.Response<PreOrderDocumentFile>>
{
    private readonly PreOrderContext _c;
    private readonly IPreOrderDocumentRenderer _renderer;
    private readonly IPreOrderDocumentStore _store;
    private readonly IPreOrderDocumentSettings _settings;

    public GetPreOrderDocumentHandler(PreOrderContext context, IPreOrderDocumentRenderer renderer, IPreOrderDocumentStore store,
        IPreOrderDocumentSettings settings)
    {
        _c = context;
        _renderer = renderer;
        _store = store;
        _settings = settings;
    }

    public async Task<Diten.CrmService.Application.Common.Models.Response<PreOrderDocumentFile>> Handle(GetPreOrderDocumentQuery request, CancellationToken ct)
    {
        if (_c.Tenant.TenantId is not { } tenantId)
        {
            return PreOrderResults.Invalid<PreOrderDocumentFile>("Tenant context is required.");
        }

        var copy = string.IsNullOrWhiteSpace(request.Copy) ? PreOrderDocumentCopies.Depot : request.Copy.Trim().ToLowerInvariant();
        if (!PreOrderDocumentCopies.IsValid(copy))
        {
            return PreOrderResults.Invalid<PreOrderDocumentFile>("copy is depot or pharmacy.");
        }

        var (order, _) = await _c.LoadVisibleAsync(tenantId, request.Id, ct);
        if (order is null)
        {
            return PreOrderResults.NotFound<PreOrderDocumentFile>();
        }

        var signature = order.Signature?.BinaryRef is { } signatureRef && signatureRef != Guid.Empty
            ? await _store.ReadAsync(tenantId, signatureRef, ct)
            : null;
        var rendered = _renderer.Render(PreOrderDocumentRules.Build(order, copy, PreOrderDocumentRules.LanguageOf(order, _settings.FallbackLanguage), signature));
        return Diten.CrmService.Application.Common.Models.Response<PreOrderDocumentFile>.Success(new PreOrderDocumentFile(rendered.Pdf, rendered.FileName));
    }
}
