using System.Net.Http.Json;
using System.Text.Json;
using Diten.Web.Models.MasterData;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers.MasterData;

/// <summary>
/// MOD-0290-FU02 Brand master UI. MdmService ([Authorize] + [HasPermission]) stays the authoritative guard;
/// the checks here are UX gating only. No DELETE action exists anywhere in this controller.
/// </summary>
[Authorize]
[Route("MasterData/Brands")]
public sealed class BrandsController : MasterDataGatewayController
{
    private const string ReadPermission = "mdm.brands.read";
    private const string CreatePermission = "mdm.brands.create";
    private const string UpdatePermission = "mdm.brands.update";
    private const string ArchivePermission = "mdm.brands.archive";
    private const string ProductReadPermission = "mdm.products.read";
    private const string ViewRoot = "~/Views/MasterData/Brands";

    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;

    public BrandsController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer,
        ILogger<BrandsController> logger)
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

        var model = new BrandEditViewModel { EffectiveFrom = DateTimeOffset.Now };
        await PopulateContractOptionsAsync(model, cancellationToken);
        return View($"{ViewRoot}/Create.cshtml", model);
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BrandEditViewModel model, CancellationToken cancellationToken)
    {
        if (RequirePage(CreatePermission) is { } denied) return denied;

        NormalizeExternalReferences(model.ExternalReferences);
        if (!ModelState.IsValid)
        {
            await PopulateContractOptionsAsync(model, cancellationToken);
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
        await PopulateContractOptionsAsync(model, cancellationToken);
        return View($"{ViewRoot}/Create.cshtml", model);
    }

    [HttpGet("{brandId:guid}/Edit")]
    [HttpGet("Edit/{brandId:guid}")]
    public async Task<IActionResult> Edit(Guid brandId, CancellationToken cancellationToken)
    {
        if (RequirePage(UpdatePermission) is { } denied) return denied;

        var brand = await LoadBrandAsync(brandId, cancellationToken);
        if (brand is null) return NotFound();

        // Archived brands are read-only (backend answers 409); the UI refuses to open the editor at all.
        if (brand.IsArchived)
        {
            TempData["WarningMessage"] = "ArchivedBrandReadOnly";
            return RedirectToAction(nameof(Details), new { brandId });
        }

        var model = ToEditModel(brand);
        await PopulateContractOptionsAsync(model, cancellationToken);
        return View($"{ViewRoot}/Edit.cshtml", model);
    }

    [HttpPost("{brandId:guid}/Edit")]
    [HttpPost("Edit/{brandId:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid brandId, BrandEditViewModel model, CancellationToken cancellationToken)
    {
        if (RequirePage(UpdatePermission) is { } denied) return denied;

        model.BrandId = brandId;
        NormalizeExternalReferences(model.ExternalReferences);
        if (!ModelState.IsValid)
        {
            await PopulateContractOptionsAsync(model, cancellationToken);
            return View($"{ViewRoot}/Edit.cshtml", model);
        }

        var response = await SendGatewayAsync(HttpMethod.Put, $"/api/mdm/brands/{brandId}", ToWritePayload(model), cancellationToken);
        if (response is not null && response.IsSuccessStatusCode)
        {
            TempData["SuccessMessage"] = _sharedLocalizer["RecordUpdated"].Value;
            return RedirectToAction(nameof(Details), new { brandId });
        }

        AddGatewayErrors(await ExtractErrorsAsync(response, _sharedLocalizer["GatewayError"].Value, cancellationToken));
        await PopulateContractOptionsAsync(model, cancellationToken);
        return View($"{ViewRoot}/Edit.cshtml", model);
    }

    [HttpGet("{brandId:guid}")]
    [HttpGet("Details/{brandId:guid}")]
    public async Task<IActionResult> Details(Guid brandId, CancellationToken cancellationToken)
    {
        if (RequirePage(ReadPermission) is { } denied) return denied;

        var brand = await LoadBrandAsync(brandId, cancellationToken);
        if (brand is null) return NotFound();

        var contract = await LoadContractAsync(cancellationToken) ?? new BrandProductContractViewModel();
        var model = new BrandPageViewModel
        {
            Brand = brand,
            Contract = contract,
            // Fail closed: with no contract every capability flag is false, so all actions stay disabled.
            CanManage = !brand.IsArchived
                        && HasAnyPermission(UpdatePermission, ArchivePermission)
                        && contract.Features.SupportsBrandManagement,
            CanReadProducts = HasAnyPermission(ProductReadPermission, ReadPermission)
                              && contract.Features.SupportsBrandProductHierarchy
        };
        return View($"{ViewRoot}/Details.cshtml", model);
    }

    // ---- same-origin browser proxy (allowlist only) ----

    [HttpGet("api/contract")]
    public Task<IActionResult> Contract(CancellationToken ct)
        => ProxyGetAsync(ContractPath, ReadPermission, ct, ProductReadPermission);

    [HttpGet("api")]
    public Task<IActionResult> List(CancellationToken ct)
        => ProxyGetAsync($"/api/mdm/brands{Request.QueryString}", ReadPermission, ct);

    [HttpGet("api/{brandId:guid}/products")]
    public Task<IActionResult> Products(Guid brandId, CancellationToken ct)
        => ProxyGetAsync($"/api/mdm/brands/{brandId}/products{Request.QueryString}", ProductReadPermission, ct, ReadPermission);

    // Archive is a POST to the archive endpoint. There is no DELETE proxy — the verb does not exist here.
    [HttpPost("api/{brandId:guid}/archive")]
    public Task<IActionResult> Archive(Guid brandId, CancellationToken ct)
        => ProxyJsonAsync(HttpMethod.Post, $"/api/mdm/brands/{brandId}/archive", null, ArchivePermission, ct);

    // ---- helpers ----

    private async Task PopulateContractOptionsAsync(BrandEditViewModel model, CancellationToken cancellationToken)
    {
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
        ExternalReferences = ToExternalReferencePayload(m.ExternalReferences)
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
        IsArchived = b.IsArchived
    };
}
