using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Commercial.Pricing;
using Diten.CrmService.Application.Features.Commercial.Pricing.FxRates;
using Diten.CrmService.Application.Features.Commercial.Pricing.PriceListImports;
using Diten.CrmService.Application.Features.Commercial.Pricing.PriceListImports.Commands;
using Diten.CrmService.Application.Features.Commercial.Pricing.PriceListImports.Queries;
using Diten.CrmService.Application.Features.Commercial.Pricing.PriceLists;
using Diten.CrmService.Application.Features.Commercial.Pricing.PriceLists.Commands;
using Diten.CrmService.Application.Features.Commercial.Pricing.PriceLists.Queries;
using Diten.CrmService.Application.Features.Commercial.Pricing.PricingHistory;
using Diten.CrmService.Application.Features.Commercial.Pricing.PricingRules;
using Diten.CrmService.Application.Features.Commercial.Pricing.PricingRules.Commands;
using Diten.CrmService.Application.Features.Commercial.Pricing.PricingRules.Queries;
using Diten.CrmService.Domain.Entities.Commercial;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Diten.CrmService.Api.Controllers.Commercial;

// WP-VW-W6 W6-BE-b — MOD-0156 §15: /api/commercial/... (its own boundary, K-O1; the Gateway routes it). TenantId is server
// resolved; every endpoint carries its commercial.* key (§14); the in-process contracts check none (§4.6.1).

[Authorize]
public sealed class PriceListsController : CustomBaseController
{
    private const string Base = "api/commercial/price-lists";
    private readonly IMediator _mediator;

    public PriceListsController(IMediator mediator) => _mediator = mediator;

    [HttpGet(Base)]
    [HasPermission(PricingPermissions.PriceListsRead)]
    public async Task<IActionResult> List([FromQuery] Guid? legalEntityId, [FromQuery] string? countryCode, [FromQuery] string? currency,
        [FromQuery] string? kind, [FromQuery] string? status, [FromQuery] string? search, [FromQuery] int? page, [FromQuery] int? pageSize,
        CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new GetPriceListListQuery(legalEntityId, countryCode, currency, kind, status, search, page, pageSize), ct));

    [HttpPost(Base)]
    [HasPermission(PricingPermissions.PriceListsCreate)]
    public async Task<IActionResult> Create([FromBody] PriceListInput input, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new CreatePriceListCommand(input), ct));

    [HttpGet(Base + "/export")]
    [HasPermission(PricingPermissions.PriceListsExport)]
    public async Task<IActionResult> Export([FromQuery] Guid? priceListId, [FromQuery] Guid? legalEntityId, [FromQuery] string? countryCode,
        [FromQuery] string? currency, [FromQuery] string? kind, [FromQuery] string? status, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetPriceListExportQuery(priceListId, legalEntityId, countryCode, currency, kind, status), ct);
        return response.IsSuccessful && response.Data is { } file
            ? File(file.Content, file.ContentType, file.FileName)
            : CreateActionResultInstance(response);
    }

    [HttpGet(Base + "/{id:guid}")]
    [HasPermission(PricingPermissions.PriceListsRead)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new GetPriceListByIdQuery(id), ct));

    [HttpPut(Base + "/{id:guid}")]
    [HasPermission(PricingPermissions.PriceListsUpdate)]
    public async Task<IActionResult> Update(Guid id, [FromBody] PriceListInput input, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new UpdatePriceListCommand(id, input), ct));

    [HttpGet(Base + "/{id:guid}/items")]
    [HasPermission(PricingPermissions.PriceListsRead)]
    public async Task<IActionResult> Items(Guid id, [FromQuery] string? search, [FromQuery] string? diff, [FromQuery] int? page, [FromQuery] int? pageSize,
        CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new GetPriceListItemsQuery(id, search, diff, page, pageSize), ct));

    [HttpPost(Base + "/{id:guid}/versions")]
    [HasPermission(PricingPermissions.PriceListsCreate)]
    public async Task<IActionResult> NewVersion(Guid id, [FromBody] PriceListVersionInput? input, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new CreatePriceListVersionCommand(id, input ?? new PriceListVersionInput(null, null)), ct));

    [HttpPost(Base + "/{id:guid}/discard")]
    [HasPermission(PricingPermissions.PriceListsCreate)]
    public async Task<IActionResult> Discard(Guid id, [FromBody] PriceListSubmitInput? input, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new DiscardPriceListCommand(id, input?.ExpectedVersion), ct));

    [HttpPost(Base + "/{id:guid}/submit")]
    [HasPermission(PricingPermissions.PriceListsSubmit)]
    public async Task<IActionResult> Submit(Guid id, [FromBody] PriceListSubmitInput? input, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new SubmitPriceListCommand(id, input?.ExpectedVersion), ct));

    [HttpPost(Base + "/{id:guid}/decision")]
    [HasPermission(PricingPermissions.PriceListsPublish)]
    public async Task<IActionResult> Decide(Guid id, [FromBody] PriceListDecisionInput input, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new DecidePriceListCommand(id, input), ct));
}

[Authorize]
public sealed class PriceListImportsController : CustomBaseController
{
    private const string Base = "api/commercial/price-list-imports";
    private readonly IMediator _mediator;

    public PriceListImportsController(IMediator mediator) => _mediator = mediator;

    /// <summary>multipart/form-data: <c>file</c> + the list header (<c>name</c>, <c>legalEntityId</c>, <c>currency</c>, <c>kind</c>,
    /// <c>validFrom</c>, <c>validTo</c>) + the column mapping (<c>gtinColumn</c>, <c>skuCodeColumn</c>, <c>depotPriceColumn</c>,
    /// <c>pharmacistPurchasePriceColumn</c>, <c>publicPriceColumn</c>, <c>vatRateColumn</c>).</summary>
    [HttpPost(Base)]
    [HasPermission(PricingPermissions.PriceListsImport)]
    [RequestSizeLimit(PricingLimits.ImportMaxBytes + 512 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = PricingLimits.ImportMaxBytes + 512 * 1024)]
    public async Task<IActionResult> Upload([FromForm] PriceListImportForm form, CancellationToken ct)
    {
        byte[]? content = null;
        if (form.File is { Length: > 0 and <= PricingLimits.ImportMaxBytes } file)
        {
            using var buffer = new MemoryStream();
            await file.CopyToAsync(buffer, ct);
            content = buffer.ToArray();
        }

        var upload = new PriceListImportUpload(form.Name, form.LegalEntityId, form.Currency, form.Kind, form.ValidFrom, form.ValidTo,
            form.File?.FileName, content,
            new PriceListFileColumns(form.GtinColumn, form.SkuCodeColumn, form.DepotPriceColumn, form.PharmacistPurchasePriceColumn,
                form.PublicPriceColumn, form.VatRateColumn));
        return CreateActionResultInstance(await _mediator.Send(new UploadPriceListImportCommand(upload), ct));
    }

    [HttpGet(Base + "/{id:guid}")]
    [HasPermission(PricingPermissions.PriceListsImport)]
    public async Task<IActionResult> Get(Guid id, [FromQuery] string? rowStatus, [FromQuery] string? diffKind, [FromQuery] int? page,
        [FromQuery] int? pageSize, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new GetPriceListImportQuery(id, rowStatus, diffKind, page, pageSize), ct));

    [HttpPost(Base + "/{id:guid}/convert")]
    [HasPermission(PricingPermissions.PriceListsImport)]
    public async Task<IActionResult> Convert(Guid id, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new ConvertPriceListImportCommand(id), ct));

    [HttpPost(Base + "/{id:guid}/discard")]
    [HasPermission(PricingPermissions.PriceListsImport)]
    public async Task<IActionResult> Discard(Guid id, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new DiscardPriceListImportCommand(id), ct));
}

public sealed class PriceListImportForm
{
    public IFormFile? File { get; set; }
    public string? Name { get; set; }
    public Guid? LegalEntityId { get; set; }
    public string? Currency { get; set; }
    public string? Kind { get; set; }
    public DateOnly? ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }
    public string? GtinColumn { get; set; }
    public string? SkuCodeColumn { get; set; }
    public string? DepotPriceColumn { get; set; }
    public string? PharmacistPurchasePriceColumn { get; set; }
    public string? PublicPriceColumn { get; set; }
    public string? VatRateColumn { get; set; }
}

[Authorize]
public sealed class PricingRulesController : CustomBaseController
{
    private const string Base = "api/commercial/pricing-rules";
    private readonly IMediator _mediator;

    public PricingRulesController(IMediator mediator) => _mediator = mediator;

    [HttpGet(Base)]
    [HasPermission(PricingPermissions.PricingRulesRead)]
    public async Task<IActionResult> List([FromQuery] Guid? legalEntityId, [FromQuery] Guid? skuId, [FromQuery] string? segmentCode,
        [FromQuery] string? status, [FromQuery] string? search, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new GetPricingRuleListQuery(legalEntityId, skuId, segmentCode, status, search, page, pageSize), ct));

    [HttpPost(Base)]
    [HasPermission(PricingPermissions.PricingRulesCreate)]
    public async Task<IActionResult> Create([FromBody] PricingRuleInput input, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new CreatePricingRuleCommand(input), ct));

    /// <summary>Kural deneme — a read on POST (the lines are a body); saves nothing.</summary>
    [HttpPost(Base + "/trial")]
    [HasPermission(PricingPermissions.PricingRulesRead)]
    public async Task<IActionResult> Trial([FromBody] PricingTrialInput input, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new EvaluatePricingTrialQuery(input), ct));

    [HttpGet(Base + "/{id:guid}")]
    [HasPermission(PricingPermissions.PricingRulesRead)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new GetPricingRuleByIdQuery(id), ct));

    [HttpPut(Base + "/{id:guid}")]
    [HasPermission(PricingPermissions.PricingRulesUpdate)]
    public async Task<IActionResult> Update(Guid id, [FromBody] PricingRuleInput input, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new UpdatePricingRuleCommand(id, input), ct));

    /// <summary><c>{ changeKind: "revise", rule: { …limits } }</c> | <c>{ changeKind: "retire" }</c> on an ACTIVE rule.</summary>
    [HttpPost(Base + "/{id:guid}/revisions")]
    [HasPermission(PricingPermissions.PricingRulesUpdate)]
    public async Task<IActionResult> Revise(Guid id, [FromBody] PricingRuleRevisionInput input, CancellationToken ct)
        => (input?.ChangeKind?.Trim().ToLowerInvariant()) switch
        {
            PricingRuleChangeKinds.Revise => CreateActionResultInstance(await _mediator.Send(
                new UpdatePricingRuleCommand(id, input.Rule ?? new PricingRuleInput(null, null, null, null, null, null, null, null, null), Revision: true), ct)),
            PricingRuleChangeKinds.Retire => CreateActionResultInstance(await _mediator.Send(new RequestPricingRuleRetirementCommand(id), ct)),
            _ => CreateActionResultInstance(Response<object>.Fail(new[] { PricingErrors.Invalid, "changeKind is revise or retire." }, 400))
        };

    [HttpPost(Base + "/{id:guid}/discard")]
    [HasPermission(PricingPermissions.PricingRulesUpdate)]
    public async Task<IActionResult> Discard(Guid id, [FromBody] PriceListSubmitInput? input, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new DiscardPricingRuleDraftCommand(id, input?.ExpectedVersion), ct));

    [HttpPost(Base + "/{id:guid}/submit")]
    [HasPermission(PricingPermissions.PricingRulesSubmit)]
    public async Task<IActionResult> Submit(Guid id, [FromBody] PriceListSubmitInput? input, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new SubmitPricingRuleCommand(id, input?.ExpectedVersion), ct));

    [HttpPost(Base + "/{id:guid}/decision")]
    [HasPermission(PricingPermissions.PricingRulesPublish)]
    public async Task<IActionResult> Decide(Guid id, [FromBody] PricingRuleDecisionInput input, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new DecidePricingRuleCommand(id, input), ct));
}

[Authorize]
public sealed class FxRatesController : CustomBaseController
{
    private const string Base = "api/commercial/fx-rates";
    private readonly IMediator _mediator;

    public FxRatesController(IMediator mediator) => _mediator = mediator;

    [HttpGet(Base)]
    [HasPermission(PricingPermissions.FxRatesRead)]
    public async Task<IActionResult> List([FromQuery] Guid? legalEntityId, [FromQuery] string? fromCurrency, [FromQuery] string? toCurrency,
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new GetFxRateListQuery(legalEntityId, fromCurrency, toCurrency, from, to, page, pageSize), ct));

    [HttpPost(Base)]
    [HasPermission(PricingPermissions.FxRatesCreate)]
    public async Task<IActionResult> Create([FromBody] FxRateInput input, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new CreateFxRateCommand(input), ct));

    [HttpPut(Base + "/{id:guid}")]
    [HasPermission(PricingPermissions.FxRatesUpdate)]
    public async Task<IActionResult> Update(Guid id, [FromBody] FxRateInput input, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new UpdateFxRateCommand(id, input), ct));

    [HttpPost(Base + "/import")]
    [HasPermission(PricingPermissions.FxRatesImport)]
    public async Task<IActionResult> Import([FromBody] FxRateImportInput input, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new ImportFxRatesCommand(input), ct));
}

/// <summary>The change history and the module dictionary: readable with ANY of the three read keys; the history shows only
/// the entity types the caller's keys cover (lists + items, rules, rates).</summary>
[Authorize]
public sealed class PricingHistoryController : CustomBaseController
{
    private readonly IMediator _mediator;

    public PricingHistoryController(IMediator mediator) => _mediator = mediator;

    public static IReadOnlyCollection<string> ReadableTypes(System.Security.Claims.ClaimsPrincipal user)
    {
        var types = new List<string>();
        if (PermissionClaims.HasPermission(user, PricingPermissions.PriceListsRead))
        {
            types.Add(PricingChangeEntityTypes.PriceList);
            types.Add(PricingChangeEntityTypes.PriceListItem);
        }

        if (PermissionClaims.HasPermission(user, PricingPermissions.PricingRulesRead))
        {
            types.Add(PricingChangeEntityTypes.PricingRule);
        }

        if (PermissionClaims.HasPermission(user, PricingPermissions.FxRatesRead))
        {
            types.Add(PricingChangeEntityTypes.FxRate);
        }

        return types;
    }

    [HttpGet("api/commercial/pricing-history")]
    public async Task<IActionResult> History([FromQuery] string? entityType, [FromQuery] Guid? entityId, [FromQuery] Guid? parentId,
        [FromQuery] string? byUserId, [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, [FromQuery] int? page, [FromQuery] int? pageSize,
        CancellationToken ct)
    {
        var types = ReadableTypes(User);
        return types.Count == 0
            ? Forbidden("Reading the pricing history needs commercial.price-lists.read, commercial.pricing-rules.read or commercial.fx-rates.read.")
            : CreateActionResultInstance(await _mediator.Send(new GetPricingChangeLogQuery(types, entityType, entityId, parentId, byUserId, from, to, page, pageSize), ct));
    }

    [HttpGet("api/commercial/pricing-contract")]
    public async Task<IActionResult> Contract(CancellationToken ct)
        => PermissionClaims.HasPermission(User, PricingPermissions.PriceListsRead) || PermissionClaims.HasPermission(User, PricingPermissions.PricingRulesRead)
            ? CreateActionResultInstance(await _mediator.Send(new GetPricingContractQuery(), ct))
            : Forbidden("Reading the pricing dictionary needs commercial.price-lists.read or commercial.pricing-rules.read.");

    private IActionResult Forbidden(string message) => CreateActionResultInstance(Response<object>.Fail(new[] { message, "permission_denied" }, 403));
}
