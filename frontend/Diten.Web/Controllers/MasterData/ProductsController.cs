using System.Net.Http.Json;
using Diten.Web.Models.MasterData;
using Diten.Web.Views.MasterData.Products;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers.MasterData;

/// <summary>
/// MOD-0290-FU02 Product master UI. MdmService stays the authoritative guard; no DELETE action exists here.
/// The brand picker is fed from the live brands endpoint — never a hardcoded list.
/// </summary>
[Authorize]
[Route("MasterData/Products")]
public sealed class ProductsController : MasterDataGatewayController
{
    private const string ReadPermission = "mdm.products.read";
    private const string CreatePermission = "mdm.products.create";
    private const string UpdatePermission = "mdm.products.update";
    private const string ArchivePermission = "mdm.products.archive";
    private const string BrandReadPermission = "mdm.brands.read";
    private const string ViewRoot = "~/Views/MasterData/Products";

    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
    private readonly IStringLocalizer<ProductsIndex> _localizer;

    public ProductsController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer,
        IStringLocalizer<ProductsIndex> localizer,
        ILogger<ProductsController> logger)
        : base(httpClient, configuration, logger)
    {
        _sharedLocalizer = sharedLocalizer;
        _localizer = localizer;
    }

    // P-2026-10-07-BRAND-FIX4 (K3) — a product refusal reaches the reader as the sentence for its reason code
    // (`Reason_{code}` in ProductsIndex.*.resx), else the status sentence; never the code, never MDM's English.
    protected override IStringLocalizer RefusalLocalizer => _localizer;

    [HttpGet("")]
    public IActionResult Index() => RequirePage(ReadPermission) ?? View($"{ViewRoot}/Index.cshtml");

    [HttpGet("Create")]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        if (RequirePage(CreatePermission) is { } denied) return denied;

        var model = new ProductEditViewModel { EffectiveFrom = CalendarDay.TodayUtc() };
        await PopulateContractOptionsAsync(model, currentBrand: null, cancellationToken);
        return View($"{ViewRoot}/Create.cshtml", model);
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProductEditViewModel model, CancellationToken cancellationToken)
    {
        if (RequirePage(CreatePermission) is { } denied) return denied;

        NormalizeExternalReferences(model.ExternalReferences);
        if (!ModelState.IsValid)
        {
            await PopulateContractOptionsAsync(model, await PostedBrandAsync(model.BrandId, cancellationToken), cancellationToken);
            return View($"{ViewRoot}/Create.cshtml", model);
        }

        var response = await SendGatewayAsync(HttpMethod.Post, "/api/mdm/products", ToWritePayload(model), cancellationToken);
        if (response is not null && response.IsSuccessStatusCode)
        {
            var envelope = await response.Content
                .ReadFromJsonAsync<BrandProductGatewayResponse<Guid>>(JsonOptions, cancellationToken);
            TempData["SuccessMessage"] = _sharedLocalizer["RecordCreated"].Value;
            return envelope?.Data is { } id && id != Guid.Empty
                ? RedirectToAction(nameof(Details), new { productId = id })
                : RedirectToAction(nameof(Index));
        }

        AddGatewayErrors(await ExtractErrorsAsync(response, _sharedLocalizer["GatewayError"].Value, cancellationToken));
        await PopulateContractOptionsAsync(model, await PostedBrandAsync(model.BrandId, cancellationToken), cancellationToken);
        return View($"{ViewRoot}/Create.cshtml", model);
    }

    [HttpGet("{productId:guid}/Edit")]
    [HttpGet("Edit/{productId:guid}")]
    public async Task<IActionResult> Edit(Guid productId, CancellationToken cancellationToken)
    {
        if (RequirePage(UpdatePermission) is { } denied) return denied;

        var product = await LoadProductAsync(productId, cancellationToken);
        if (product is null) return NotFound();

        if (product.IsArchived)
        {
            TempData["WarningMessage"] = "ArchivedProductReadOnly";
            return RedirectToAction(nameof(Details), new { productId });
        }

        var model = ToEditModel(product);
        await PopulateContractOptionsAsync(model, await CurrentBrandAsync(product, cancellationToken), cancellationToken);
        return View($"{ViewRoot}/Edit.cshtml", model);
    }

    [HttpPost("{productId:guid}/Edit")]
    [HttpPost("Edit/{productId:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid productId, ProductEditViewModel model, CancellationToken cancellationToken)
    {
        if (RequirePage(UpdatePermission) is { } denied) return denied;

        model.ProductId = productId;
        NormalizeExternalReferences(model.ExternalReferences);
        if (!ModelState.IsValid)
        {
            await PopulateEditOptionsAsync(model, cancellationToken);
            return View($"{ViewRoot}/Edit.cshtml", model);
        }

        var response = await SendGatewayAsync(HttpMethod.Put, $"/api/mdm/products/{productId}", ToWritePayload(model), cancellationToken);
        if (response is not null && response.IsSuccessStatusCode)
        {
            TempData["SuccessMessage"] = _sharedLocalizer["RecordUpdated"].Value;
            return RedirectToAction(nameof(Details), new { productId });
        }

        AddGatewayErrors(await ExtractErrorsAsync(response, _sharedLocalizer["GatewayError"].Value, cancellationToken));
        await PopulateEditOptionsAsync(model, cancellationToken);
        return View($"{ViewRoot}/Edit.cshtml", model);
    }

    [HttpGet("{productId:guid}")]
    [HttpGet("Details/{productId:guid}")]
    public async Task<IActionResult> Details(Guid productId, CancellationToken cancellationToken)
    {
        if (RequirePage(ReadPermission) is { } denied) return denied;

        var product = await LoadProductAsync(productId, cancellationToken);
        if (product is null) return NotFound();

        var contract = await LoadContractAsync(cancellationToken) ?? new BrandProductContractViewModel();
        var model = new ProductPageViewModel
        {
            Product = product,
            Contract = contract,
            CanManage = !product.IsArchived
                        && HasAnyPermission(UpdatePermission, ArchivePermission)
                        && contract.Features.SupportsProductManagement,
            // Resolved only when the operator may read brands. If it stays null the view shows the raw
            // BrandId — it never fabricates a display name.
            Brand = await TryResolveBrandAsync(product.BrandId, cancellationToken)
        };
        return View($"{ViewRoot}/Details.cshtml", model);
    }

    // ---- same-origin browser proxy (allowlist only) ----

    [HttpGet("api/contract")]
    public Task<IActionResult> Contract(CancellationToken ct)
        => ProxyGetAsync(ContractPath, ReadPermission, ct, BrandReadPermission);

    [HttpGet("api")]
    public Task<IActionResult> List(CancellationToken ct)
        => ProxyGetAsync($"/api/mdm/products{Request.QueryString}", ReadPermission, ct);

    /// <summary>Feeds the brand filter/picker. Read-only passthrough to the brands list endpoint.</summary>
    [HttpGet("api/brands")]
    public Task<IActionResult> Brands(CancellationToken ct)
        => ProxyGetAsync($"/api/mdm/brands{Request.QueryString}", BrandReadPermission, ct, ReadPermission);

    // WP-MDM-BRAND-ACCEPT-01 FIX2 (M9) — a cross-site POST cannot archive: the page's anti-forgery token is required.
    [HttpPost("api/{productId:guid}/archive")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Archive(Guid productId, CancellationToken ct)
        => ProxyJsonAsync(HttpMethod.Post, $"/api/mdm/products/{productId}/archive", null, ArchivePermission, ct);

    // ---- helpers ----

    private async Task PopulateContractOptionsAsync(
        ProductEditViewModel model, BrandOptionViewModel? currentBrand, CancellationToken cancellationToken)
    {
        var contract = await LoadContractAsync(cancellationToken);
        if (contract is null || !contract.IsReady || !contract.Features.SupportsProductManagement)
        {
            // FIX4 (F3) — no early return: the brand options are still built below, so a form re-rendered without the
            // contract keeps the posted brand selected and a save never drops it.
            model.ContractError = "BrandProductContractUnavailable";
        }
        else
        {
            model.ProductStatuses = contract.Vocabulary.ProductStatuses;
            model.ProductTypes = contract.Vocabulary.ProductTypes;
            model.DosageForms = contract.Vocabulary.DosageForms;
            model.UnitsOfMeasure = contract.Vocabulary.UnitsOfMeasure;
        }

        // P-2026-10-07-BRAND-FIX3 (R-4) — an editor who may not read brands sees only the product's current brand, read-only;
        // the brand list is not asked. A hidden input in the form carries BrandId, so the save keeps it.
        model.BrandReadOnly = currentBrand is not null && !HasAnyPermission(BrandReadPermission);
        if (model.BrandReadOnly)
        {
            model.BrandOptions = [currentBrand!];
            return;
        }

        var options = (await LoadLinkableBrandOptionsAsync(cancellationToken)).ToList();
        // R-4 — the current brand is never dropped: if MDM would not link it any more it is still offered (and selected by
        // BrandId), marked "(not linkable)", so saving without touching the brand posts the same BrandId.
        if (currentBrand is not null && options.All(x => x.BrandId != currentBrand.BrandId))
        {
            options.Insert(0, currentBrand);
        }

        model.BrandOptions = options;
    }

    /// <summary>
    /// A re-rendered edit (local validation or an MDM refusal such as 409 version_conflict, or a failed PUT): the picker
    /// keeps the brand the form POSTED — never a different one, never "No brand". Its name comes from the product read
    /// (MDM's linkedBrand) when that read still answers with the same brand, otherwise from <see cref="PostedBrandAsync"/>.
    /// </summary>
    private async Task PopulateEditOptionsAsync(ProductEditViewModel model, CancellationToken cancellationToken)
    {
        BrandOptionViewModel? currentBrand = null;
        if (model.BrandId is { } postedBrandId && postedBrandId != Guid.Empty)
        {
            if (model.ProductId is { } productId
                && await LoadProductAsync(productId, cancellationToken) is { } product
                && product.BrandId == postedBrandId)
            {
                currentBrand = await CurrentBrandAsync(product, cancellationToken);
            }

            // P-2026-10-07-BRAND-FIX4 (F3) — the product read failed (502/503/timeout) or answered another brand: the
            // option is built from the POSTED BrandId, so the next save posts the same BrandId instead of none.
            currentBrand ??= await PostedBrandAsync(postedBrandId, cancellationToken);
        }

        await PopulateContractOptionsAsync(model, currentBrand, cancellationToken);
    }

    /// <summary>
    /// FIX4 (F3) — an option for a posted BrandId: the brand read when the operator may read brands and it answers,
    /// otherwise the id itself marked "(could not be read)". Null only when nothing was posted.
    /// </summary>
    private async Task<BrandOptionViewModel?> PostedBrandAsync(Guid? postedBrandId, CancellationToken cancellationToken)
    {
        if (postedBrandId is not { } id || id == Guid.Empty)
        {
            return null;
        }

        return await TryResolveBrandAsync(id, cancellationToken) ?? UnreadableBrand(id);
    }

    /// <summary>FIX4 (F3) — a brand whose name could not be read: its id, marked; never a fabricated name.</summary>
    private static BrandOptionViewModel UnreadableBrand(Guid brandId)
        => new(brandId, brandId.ToString("D"), string.Empty, NameUnreadable: true);

    /// <summary>
    /// The brands MDM would link today — MDM decides (isLinkable: active + inside its validity window, UTC); the web keeps
    /// no copy of that rule (R-5). Requires brand read permission; without it the list is never asked.
    /// </summary>
    private async Task<IReadOnlyList<BrandOptionViewModel>> LoadLinkableBrandOptionsAsync(CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(BrandReadPermission))
        {
            return [];
        }

        var response = await SendGatewayAsync(
            HttpMethod.Get, "/api/mdm/brands?brandStatus=active&includeArchived=false", null, cancellationToken);
        if (response is null || !response.IsSuccessStatusCode)
        {
            return [];
        }

        var envelope = await response.Content
            .ReadFromJsonAsync<BrandProductGatewayResponse<BrandListResultViewModel>>(JsonOptions, cancellationToken);

        return (envelope?.Data?.Items ?? [])
            .Where(x => x.IsLinkable == true)
            .Select(x => new BrandOptionViewModel(x.BrandId, x.BrandCode, x.BrandName))
            .ToList();
    }

    /// <summary>
    /// R-4 — the product's current brand as an option. MDM's linkedBrand first (readable with mdm.products.read alone);
    /// an MDM build that does not send it yet falls back to the brand read (brand readers only), and otherwise to the raw
    /// BrandId — never a fabricated name.
    /// </summary>
    private async Task<BrandOptionViewModel?> CurrentBrandAsync(ProductDetailViewModel product, CancellationToken cancellationToken)
    {
        if (product.BrandId is not { } brandId || brandId == Guid.Empty)
        {
            return null;
        }

        if (product.LinkedBrand is { } linked && linked.BrandId == brandId)
        {
            return new BrandOptionViewModel(linked.BrandId, linked.BrandCode, linked.BrandName, IsLinkable: linked.IsLinkable);
        }

        return await TryResolveBrandAsync(brandId, cancellationToken) ?? UnreadableBrand(brandId);
    }

    private async Task<BrandOptionViewModel?> TryResolveBrandAsync(Guid? brandId, CancellationToken cancellationToken)
    {
        if (brandId is not { } id || id == Guid.Empty || !HasAnyPermission(BrandReadPermission))
        {
            return null;
        }

        var response = await SendGatewayAsync(HttpMethod.Get, $"/api/mdm/brands/{id}", null, cancellationToken);
        if (response is null || !response.IsSuccessStatusCode)
        {
            return null;
        }

        var brand = (await response.Content
            .ReadFromJsonAsync<BrandProductGatewayResponse<BrandDetailViewModel>>(JsonOptions, cancellationToken))?.Data;

        return brand is null
            ? null
            : new BrandOptionViewModel(brand.BrandId, brand.BrandCode, brand.BrandName, brand.IsArchived, brand.IsLinkable == true);
    }

    private async Task<ProductDetailViewModel?> LoadProductAsync(Guid productId, CancellationToken cancellationToken)
    {
        var response = await SendGatewayAsync(HttpMethod.Get, $"/api/mdm/products/{productId}", null, cancellationToken);
        if (response is null || !response.IsSuccessStatusCode) return null;

        return (await response.Content
            .ReadFromJsonAsync<BrandProductGatewayResponse<ProductDetailViewModel>>(JsonOptions, cancellationToken))?.Data;
    }

    private static object ToWritePayload(ProductEditViewModel m) => new
    {
        m.ProductCode,
        m.ProductName,
        m.ProductStatus,
        m.BrandId,
        m.ProductType,
        m.DosageForm,
        m.Strength,
        m.PackSize,
        m.UnitOfMeasure,
        m.ATCCode,
        m.TherapeuticAreaId,
        IndicationRefs = m.ParseIndicationRefs(),
        m.Description,
        EffectiveFrom = m.EffectiveFrom,
        EffectiveTo = m.EffectiveTo,
        ExternalReferences = ToExternalReferencePayload(m.ExternalReferences),
        // R-3 — required by MDM on update (missing → 400 expected_version_required, stale → 409 version_conflict); null on create.
        m.ExpectedVersion
        // No TenantId — deliberately absent from the payload shape.
    };

    private static ProductEditViewModel ToEditModel(ProductDetailViewModel p) => new()
    {
        ProductId = p.ProductId,
        ProductCode = p.ProductCode,
        ProductName = p.ProductName,
        ProductStatus = p.ProductStatus,
        BrandId = p.BrandId,
        ProductType = p.ProductType,
        DosageForm = p.DosageForm,
        Strength = p.Strength,
        PackSize = p.PackSize,
        UnitOfMeasure = p.UnitOfMeasure,
        ATCCode = p.ATCCode,
        TherapeuticAreaId = p.TherapeuticAreaId,
        IndicationRefsRaw = string.Join(", ", p.IndicationRefs),
        Description = p.Description,
        EffectiveFrom = p.EffectiveFrom,
        EffectiveTo = p.EffectiveTo,
        ExternalReferences = p.ExternalReferences,
        IsArchived = p.IsArchived,
        ExpectedVersion = p.Version
    };
}
