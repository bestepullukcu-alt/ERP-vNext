using System.Net.Http.Json;
using Diten.Web.Models.MasterData;
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

    public ProductsController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer,
        ILogger<ProductsController> logger)
        : base(httpClient, configuration, logger)
    {
        _sharedLocalizer = sharedLocalizer;
    }

    [HttpGet("")]
    public IActionResult Index() => RequirePage(ReadPermission) ?? View($"{ViewRoot}/Index.cshtml");

    [HttpGet("Create")]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        if (RequirePage(CreatePermission) is { } denied) return denied;

        var model = new ProductEditViewModel { EffectiveFrom = DateTimeOffset.Now };
        await PopulateContractOptionsAsync(model, cancellationToken);
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
            await PopulateContractOptionsAsync(model, cancellationToken);
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
        await PopulateContractOptionsAsync(model, cancellationToken);
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
        await PopulateContractOptionsAsync(model, cancellationToken);
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
            await PopulateContractOptionsAsync(model, cancellationToken);
            return View($"{ViewRoot}/Edit.cshtml", model);
        }

        var response = await SendGatewayAsync(HttpMethod.Put, $"/api/mdm/products/{productId}", ToWritePayload(model), cancellationToken);
        if (response is not null && response.IsSuccessStatusCode)
        {
            TempData["SuccessMessage"] = _sharedLocalizer["RecordUpdated"].Value;
            return RedirectToAction(nameof(Details), new { productId });
        }

        AddGatewayErrors(await ExtractErrorsAsync(response, _sharedLocalizer["GatewayError"].Value, cancellationToken));
        await PopulateContractOptionsAsync(model, cancellationToken);
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

    [HttpPost("api/{productId:guid}/archive")]
    public Task<IActionResult> Archive(Guid productId, CancellationToken ct)
        => ProxyJsonAsync(HttpMethod.Post, $"/api/mdm/products/{productId}/archive", null, ArchivePermission, ct);

    // ---- helpers ----

    private async Task PopulateContractOptionsAsync(ProductEditViewModel model, CancellationToken cancellationToken)
    {
        var contract = await LoadContractAsync(cancellationToken);
        if (contract is null || !contract.IsReady || !contract.Features.SupportsProductManagement)
        {
            model.ContractError = "BrandProductContractUnavailable";
            return;
        }

        model.ProductStatuses = contract.Vocabulary.ProductStatuses;
        model.ProductTypes = contract.Vocabulary.ProductTypes;
        model.DosageForms = contract.Vocabulary.DosageForms;
        model.UnitsOfMeasure = contract.Vocabulary.UnitsOfMeasure;
        model.BrandOptions = await LoadActiveBrandOptionsAsync(cancellationToken);
    }

    /// <summary>
    /// Active, non-archived brands only — an archived brand cannot receive new product links (409), so it is
    /// never offered as a choice. Requires brand read permission; without it the picker is simply empty and the
    /// operator can still create a brand-less product (BrandId is optional).
    /// </summary>
    private async Task<IReadOnlyList<BrandOptionViewModel>> LoadActiveBrandOptionsAsync(CancellationToken cancellationToken)
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
            .Where(x => !x.IsArchived)
            .Select(x => new BrandOptionViewModel(x.BrandId, x.BrandCode, x.BrandName))
            .ToList();
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

        return brand is null ? null : new BrandOptionViewModel(brand.BrandId, brand.BrandCode, brand.BrandName);
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
        ExternalReferences = ToExternalReferencePayload(m.ExternalReferences)
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
        IsArchived = p.IsArchived
    };
}
