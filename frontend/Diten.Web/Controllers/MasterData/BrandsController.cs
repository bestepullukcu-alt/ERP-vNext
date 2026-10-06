using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.Web.Models.MasterData;
using Diten.Web.Views.MasterData.Brands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers.MasterData;

/// <summary>
/// MOD-0290-FU02 Brand master UI. MdmService ([Authorize] + [HasPermission]) stays the authoritative guard;
/// the checks here are UX gating only. No DELETE action exists anywhere in this controller.
///
/// WP-MDM-BRAND-ACCEPT-01 FIX1:
/// <list type="bullet">
/// <item>item 5 (UAS-001) — a signed-in reader without the page's key gets the page's OWN 403 inside the shell (the
/// view draws the access-denied surface and nothing else); never an empty 403, never a redirect.</item>
/// <item>item 6 — the list is the list component in server mode: this proxy turns DataTables' start / length / search /
/// orderBy / orderDir into MDM's paged read and answers in the component's shape.</item>
/// <item>item 7 — every refusal is the reader's own language (<see cref="RefusalLocalizer"/>).</item>
/// <item>item 8 — the owner company is a NAME (picked from the legal-entity lookup), never an id.</item>
/// <item>item 11 — every state-changing proxy validates the anti-forgery token.</item>
/// <item>item 13 — the page's actions are decided here through PermissionClaims, the same gate the proxies use.</item>
/// </list>
/// </summary>
[Authorize]
[Route("MasterData/Brands")]
public sealed class BrandsController : MasterDataGatewayController
{
    private const string ReadPermission = "mdm.brands.read";
    private const string CreatePermission = "mdm.brands.create";
    private const string UpdatePermission = "mdm.brands.update";
    private const string ArchivePermission = "mdm.brands.archive";
    // WP-MDM-BRAND-ACCEPT-01 FIX2 (item 1) — the server export's own key (brand-product-master).
    private const string ExportPermission = "mdm.brands.export";
    private const string ProductReadPermission = "mdm.products.read";
    private const string LegalEntityLookupPath = "/api/legal-entities/lookup";
    private const string ViewRoot = "~/Views/MasterData/Brands";

    /// <summary>FIX1 (item 6) — the register's page size bound; MDM refuses more (GetBrandListQuery.MaxLength).</summary>
    internal const int MaxListLength = 100;
    private const int DefaultListLength = 20;

    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
    private readonly IStringLocalizer<BrandsIndex> _localizer;

    public BrandsController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer,
        IStringLocalizer<BrandsIndex> localizer,
        ILogger<BrandsController> logger)
        : base(httpClient, configuration, logger)
    {
        _sharedLocalizer = sharedLocalizer;
        _localizer = localizer;
    }

    protected override IStringLocalizer RefusalLocalizer => _localizer;

    [HttpGet("")]
    public IActionResult Index()
    {
        if (!HasAnyPermission(ReadPermission))
        {
            return Denied("Index", null);
        }

        ViewData["CanCreateBrand"] = HasAnyPermission(CreatePermission);
        ViewData["CanUpdateBrand"] = HasAnyPermission(UpdatePermission);
        ViewData["CanArchiveBrand"] = HasAnyPermission(ArchivePermission);
        ViewData["CanExportBrand"] = HasAnyPermission(ExportPermission);
        return View($"{ViewRoot}/Index.cshtml");
    }

    [HttpGet("Create")]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(CreatePermission))
        {
            return Denied("Create", new BrandEditViewModel());
        }

        var model = new BrandEditViewModel { EffectiveFrom = DateTimeOffset.Now };
        await PopulateFormOptionsAsync(model, cancellationToken);
        return View($"{ViewRoot}/Create.cshtml", model);
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BrandEditViewModel model, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(CreatePermission))
        {
            return Denied("Create", new BrandEditViewModel());
        }

        NormalizeExternalReferences(model.ExternalReferences);
        if (!ModelState.IsValid)
        {
            await PopulateFormOptionsAsync(model, cancellationToken);
            return View($"{ViewRoot}/Create.cshtml", model);
        }

        var response = await SendGatewayAsync(HttpMethod.Post, "/api/mdm/brands", ToWritePayload(model), cancellationToken);
        if (response is not null && response.IsSuccessStatusCode)
        {
            var envelope = await response.Content
                .ReadFromJsonAsync<BrandProductGatewayResponse<Guid>>(JsonOptions, cancellationToken);
            TempData["SuccessMessage"] = _sharedLocalizer["RecordCreated"].Value;
            return envelope?.Data is { } id && id != Guid.Empty
                ? RedirectToAction(nameof(Details), new { brandId = id })
                : RedirectToAction(nameof(Index));
        }

        AddGatewayErrors(await ExtractErrorsAsync(response, _sharedLocalizer["GatewayError"].Value, cancellationToken));
        await PopulateFormOptionsAsync(model, cancellationToken);
        return View($"{ViewRoot}/Create.cshtml", model);
    }

    [HttpGet("{brandId:guid}/Edit")]
    [HttpGet("Edit/{brandId:guid}")]
    public async Task<IActionResult> Edit(Guid brandId, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(UpdatePermission))
        {
            return Denied("Edit", new BrandEditViewModel { BrandId = brandId });
        }

        var brand = await LoadBrandAsync(brandId, cancellationToken);
        if (brand is null) return NotFound();

        // Archived brands are read-only (backend answers 409); the UI refuses to open the editor at all.
        if (brand.IsArchived)
        {
            TempData["WarningMessage"] = "ArchivedBrandReadOnly";
            return RedirectToAction(nameof(Details), new { brandId });
        }

        var model = ToEditModel(brand);
        await PopulateFormOptionsAsync(model, cancellationToken);
        return View($"{ViewRoot}/Edit.cshtml", model);
    }

    [HttpPost("{brandId:guid}/Edit")]
    [HttpPost("Edit/{brandId:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid brandId, BrandEditViewModel model, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(UpdatePermission))
        {
            return Denied("Edit", new BrandEditViewModel { BrandId = brandId });
        }

        model.BrandId = brandId;
        NormalizeExternalReferences(model.ExternalReferences);
        if (!ModelState.IsValid)
        {
            await PopulateFormOptionsAsync(model, cancellationToken);
            return View($"{ViewRoot}/Edit.cshtml", model);
        }

        var response = await SendGatewayAsync(HttpMethod.Put, $"/api/mdm/brands/{brandId}", ToWritePayload(model), cancellationToken);
        if (response is not null && response.IsSuccessStatusCode)
        {
            TempData["SuccessMessage"] = _sharedLocalizer["RecordUpdated"].Value;
            return RedirectToAction(nameof(Details), new { brandId });
        }

        AddGatewayErrors(await ExtractErrorsAsync(response, _sharedLocalizer["GatewayError"].Value, cancellationToken));
        await PopulateFormOptionsAsync(model, cancellationToken);
        return View($"{ViewRoot}/Edit.cshtml", model);
    }

    [HttpGet("{brandId:guid}")]
    [HttpGet("Details/{brandId:guid}")]
    public async Task<IActionResult> Details(Guid brandId, CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(ReadPermission))
        {
            return Denied("Details", null);
        }

        var brand = await LoadBrandAsync(brandId, cancellationToken);
        if (brand is null) return NotFound();

        var contract = await LoadContractAsync(cancellationToken) ?? new BrandProductContractViewModel();
        // Fail closed: with no contract every capability flag is false, so all actions stay disabled.
        var manageable = !brand.IsArchived && contract.Features.SupportsBrandManagement;
        var model = new BrandPageViewModel
        {
            Brand = brand,
            Contract = contract,
            CanUpdate = manageable && HasAnyPermission(UpdatePermission),
            CanArchive = manageable && HasAnyPermission(ArchivePermission),
            CanReadProducts = HasAnyPermission(ProductReadPermission, ReadPermission)
                              && contract.Features.SupportsBrandProductHierarchy,
            OwnerCompanyName = brand.OwnerCompanyId is { } ownerId
                ? (await LoadOwnerCompaniesAsync(cancellationToken))?.FirstOrDefault(x => x.LegalEntityId == ownerId) is { } owner
                    ? $"{owner.Code} — {owner.Name}"
                    : null
                : null
        };
        model.CanManage = model.CanUpdate || model.CanArchive;
        return View($"{ViewRoot}/Details.cshtml", model);
    }

    // ---- same-origin browser proxy (allowlist only) ----

    [HttpGet("api/contract")]
    public Task<IActionResult> Contract(CancellationToken ct)
        => ProxyGetAsync(ContractPath, ReadPermission, ct, ProductReadPermission);

    /// <summary>
    /// FIX1 (item 6) — the register's page. The list component sends start / length / search / orderBy / orderDir and the
    /// inline filters; MDM sorts, filters and cuts on the server and this answers in the component's shape. (The CRM
    /// pickers do not come through here — they read MDM through their own proxies and keep receiving every brand.)
    /// </summary>
    [HttpGet("api")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (RequireJson(ReadPermission) is { } denied)
        {
            return denied;
        }

        var response = await SendGatewayAsync(HttpMethod.Get, $"/api/mdm/brands?{BuildListQuery(Request.Query)}", null, ct);
        if (response is null || !response.IsSuccessStatusCode)
        {
            return await RelayAsync(response, ct);
        }

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        if (!document.RootElement.TryGetProperty("data", out var page)
            || !page.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array
            || !page.TryGetProperty("totalCount", out var count) || !count.TryGetInt64(out var total))
        {
            return await RelayAsync(null, ct);
        }

        int? archivedWithLiveProducts = page.TryGetProperty("archivedWithLiveProducts", out var kpi) && kpi.TryGetInt32(out var value)
            ? value
            : null;
        return new ContentResult
        {
            StatusCode = StatusCodes.Status200OK,
            ContentType = "application/json",
            Content = JsonSerializer.Serialize(new
            {
                success = true,
                data = new { items = items.Clone(), total, filteredTotal = total, archivedWithLiveProducts }
            }, JsonOptions)
        };
    }

    internal static string BuildListQuery(IQueryCollection query)
    {
        var length = int.TryParse(query["length"].ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedLength)
                     && parsedLength > 0
            ? Math.Min(parsedLength, MaxListLength)
            : DefaultListLength;
        var start = int.TryParse(query["start"].ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedStart)
                    && parsedStart > 0
            ? parsedStart
            : 0;
        var parts = new List<string>
        {
            $"start={start.ToString(CultureInfo.InvariantCulture)}",
            $"length={length.ToString(CultureInfo.InvariantCulture)}"
        };
        // Passed as typed; MDM is the one judge (an unknown sort key or direction is a 400, never a silent default).
        // FIX2 (M6) — the business unit / therapeutic area free-text GUID boxes are gone from the filter; with no master to
        // pick from, those two filters are not offered (and not forwarded) until a picker exists.
        foreach (var key in new[] { "search", "orderBy", "orderDir", "brandStatus", "includeArchived" })
        {
            var value = query[key].ToString().Trim();
            if (value.Length > 0)
            {
                parts.Add($"{key}={Uri.EscapeDataString(value)}");
            }
        }

        return string.Join("&", parts);
    }

    /// <summary>
    /// FIX2 (item 1, BL-452) — the register's server export: every matching row as CSV / XLSX, written by MDM in the reader's
    /// language. Without mdm.brands.export the page draws no file entry and this answers 403 (UAS-001: no redirect).
    /// </summary>
    [HttpGet("api/export")]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        if (RequireJson(ExportPermission) is { } denied)
        {
            return denied;
        }

        var acceptLanguage = Request.Headers.AcceptLanguage.ToString();
        var response = await SendGatewayAsync(HttpMethod.Get, $"/api/mdm/brands/export?{BuildExportQuery(Request.Query)}", null, ct,
            request =>
            {
                if (acceptLanguage.Length > 0)
                {
                    request.Headers.TryAddWithoutValidation("Accept-Language", acceptLanguage);
                }
            });
        if (response is null || !response.IsSuccessStatusCode)
        {
            return await RelayAsync(response, ct);
        }

        var content = await response.Content.ReadAsByteArrayAsync(ct);
        var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
                       ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                       ?? "brands";
        return File(content, contentType, fileName);
    }

    /// <summary>The export's query: the list's own search / order / filters, the format and the columns. Never start / length.</summary>
    internal static string BuildExportQuery(IQueryCollection query)
    {
        var parts = new List<string>();
        foreach (var key in new[] { "format", "columns", "search", "orderBy", "orderDir", "brandStatus", "includeArchived" })
        {
            foreach (var raw in query[key])
            {
                var value = raw?.Trim() ?? string.Empty;
                if (value.Length > 0)
                {
                    parts.Add($"{key}={Uri.EscapeDataString(value)}");
                }
            }
        }

        return string.Join("&", parts);
    }

    /// <summary>FIX1 (item 3) — one brand as JSON (the archive confirmation reads its live product count fresh).</summary>
    [HttpGet("api/{brandId:guid}/detail")]
    public Task<IActionResult> Detail(Guid brandId, CancellationToken ct)
        => ProxyGetAsync($"/api/mdm/brands/{brandId}", ReadPermission, ct);

    [HttpGet("api/{brandId:guid}/products")]
    public Task<IActionResult> Products(Guid brandId, CancellationToken ct)
        => ProxyGetAsync($"/api/mdm/brands/{brandId}/products{Request.QueryString}", ProductReadPermission, ct, ReadPermission);

    // Archive is a POST to the archive endpoint. There is no DELETE proxy — the verb does not exist here.
    // FIX1 (item 11) — a cross-site POST cannot archive: the page's anti-forgery token is required.
    [HttpPost("api/{brandId:guid}/archive")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Archive(Guid brandId, CancellationToken ct)
        => ProxyJsonAsync(HttpMethod.Post, $"/api/mdm/brands/{brandId}/archive", null, ArchivePermission, ct);

    // ---- helpers ----

    /// <summary>FIX1 (item 5) — the page's own 403: the view draws the access-denied surface inside the shell.</summary>
    private ViewResult Denied(string view, object? model)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View($"{ViewRoot}/{view}.cshtml", model);
    }

    private async Task PopulateFormOptionsAsync(BrandEditViewModel model, CancellationToken cancellationToken)
    {
        var owners = await LoadOwnerCompaniesAsync(cancellationToken);
        model.OwnerCompanies = owners ?? [];
        model.OwnerCompaniesUnavailable = owners is null;

        var contract = await LoadContractAsync(cancellationToken);
        if (contract is null || !contract.IsReady || !contract.Features.SupportsBrandManagement)
        {
            model.ContractError = "BrandProductContractUnavailable";
            return;
        }

        // Status options come from the contract, never from a hardcoded list — so the MOD-0048 reconciliation
        // can change the vocabulary source without touching this view.
        model.BrandStatuses = contract.Vocabulary.BrandStatuses;
    }

    /// <summary>FIX1 (item 8) — the tenant's legal entities by code and name; null when they cannot be read.</summary>
    private async Task<IReadOnlyList<LegalEntityOptionViewModel>?> LoadOwnerCompaniesAsync(CancellationToken cancellationToken)
    {
        var response = await SendGatewayAsync(HttpMethod.Get, LegalEntityLookupPath, null, cancellationToken);
        if (response is null || !response.IsSuccessStatusCode)
        {
            return null;
        }

        try
        {
            var envelope = await response.Content
                .ReadFromJsonAsync<BrandProductGatewayResponse<List<LegalEntityLookupItem>>>(JsonOptions, cancellationToken);
            return envelope?.Data?
                .Select(x => new LegalEntityOptionViewModel(x.LegalEntityId, x.Code, x.DisplayName ?? x.LegalName))
                .OrderBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record LegalEntityLookupItem(Guid LegalEntityId, string Code, string LegalName, string? DisplayName);

    private async Task<BrandDetailViewModel?> LoadBrandAsync(Guid brandId, CancellationToken cancellationToken)
    {
        var response = await SendGatewayAsync(HttpMethod.Get, $"/api/mdm/brands/{brandId}", null, cancellationToken);
        if (response is null || !response.IsSuccessStatusCode) return null;

        return (await response.Content
            .ReadFromJsonAsync<BrandProductGatewayResponse<BrandDetailViewModel>>(JsonOptions, cancellationToken))?.Data;
    }

    private static object ToWritePayload(BrandEditViewModel m) => new
    {
        m.BrandCode,
        m.BrandName,
        m.BrandStatus,
        m.Description,
        m.OwnerCompanyId,
        m.BusinessUnitId,
        m.TherapeuticAreaId,
        EffectiveFrom = m.EffectiveFrom,
        EffectiveTo = m.EffectiveTo,
        ExternalReferences = ToExternalReferencePayload(m.ExternalReferences),
        // FIX1 (item 2) — the read this edit was made on (null on create, where MDM ignores it).
        m.ExpectedVersion
        // No TenantId — deliberately absent from the payload shape.
    };

    private static BrandEditViewModel ToEditModel(BrandDetailViewModel b) => new()
    {
        BrandId = b.BrandId,
        BrandCode = b.BrandCode,
        BrandName = b.BrandName,
        BrandStatus = b.BrandStatus,
        Description = b.Description,
        OwnerCompanyId = b.OwnerCompanyId,
        BusinessUnitId = b.BusinessUnitId,
        TherapeuticAreaId = b.TherapeuticAreaId,
        EffectiveFrom = b.EffectiveFrom,
        EffectiveTo = b.EffectiveTo,
        ExternalReferences = b.ExternalReferences,
        IsArchived = b.IsArchived,
        ExpectedVersion = b.Version
    };
}
