using ClosedXML.Excel;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Commercial.PreOrders.Handlers.CommandHandlers;
using Diten.CrmService.Application.Features.Commercial.PreOrders.Queries;
using Diten.CrmService.Application.Features.Commercial.PreOrders.Validators;
using Diten.CrmService.Application.Features.Commercial.Pricing.Contracts;
using Diten.CrmService.Domain.Entities.Commercial;
using Diten.CrmService.Domain.Repositories.Commercial;
using MediatR;

namespace Diten.CrmService.Application.Features.Commercial.PreOrders.Handlers.QueryHandlers;

/// <summary>WP-VW-W6 W6-BE-a — MOD-0168 reads. Writes nothing except the reconcile-on-read of a pending approval.</summary>
public sealed class PreOrderQueryHandlers :
    IRequestHandler<GetPreOrderListQuery, Response<PreOrderPageDto>>,
    IRequestHandler<GetPreOrderByIdQuery, Response<PreOrderDto>>,
    IRequestHandler<GetPreOrderContextQuery, Response<PreOrderContextDto>>,
    IRequestHandler<PreviewPreOrderQuery, Response<PreOrderPreviewDto>>,
    IRequestHandler<GetPharmacyOrderHistoryQuery, Response<PreOrderPharmacyHistoryDto>>,
    IRequestHandler<GetPreOrderSettingsQuery, Response<PreOrderSettingsDto>>,
    IRequestHandler<ResolvePreOrderApprovalLinkQuery, Response<PreOrderApprovalLinkDto>>,
    IRequestHandler<GetPreOrderExportQuery, Response<PreOrderExportDto>>
{
    private readonly PreOrderContext _c;
    private readonly PreOrderParts _parts;
    private readonly PreOrderPricing _pricing;
    private readonly PreOrderApprovalService _approval;

    public PreOrderQueryHandlers(PreOrderContext context, PreOrderParts parts, PreOrderPricing pricing, PreOrderApprovalService approval)
    {
        _c = context;
        _parts = parts;
        _pricing = pricing;
        _approval = approval;
    }

    public async Task<Response<PreOrderPageDto>> Handle(GetPreOrderListQuery request, CancellationToken ct)
    {
        if (_c.Tenant.TenantId is not { } tenantId)
        {
            return PreOrderResults.Invalid<PreOrderPageDto>("Tenant context is required.");
        }

        var (filter, problem) = await FilterAsync(tenantId, request, ct);
        if (problem is not null)
        {
            return PreOrderResults.Invalid<PreOrderPageDto>(problem);
        }

        var page = Math.Max(1, request.Page ?? 1);
        var size = Math.Clamp(request.PageSize ?? 25, 1, PreOrderLimits.PageSizeMax);
        var (items, total) = await _c.Orders.ListAsync(tenantId, filter!, (page - 1) * size, size, ct);
        return Response<PreOrderPageDto>.Success(new PreOrderPageDto(items.Select(o => PreOrderMapping.ToRow(o, _c.CallerId)).ToList(), total, page, size));
    }

    /// <summary>The caller's scope as a filter: read-all = every rep; else own + team reps, OR resolved as the approver.</summary>
    private async Task<(PreOrderFilter? Filter, string? Problem)> FilterAsync(Guid tenantId, GetPreOrderListQuery q, CancellationToken ct)
    {
        IReadOnlyCollection<string>? statuses = null;
        if (!string.IsNullOrWhiteSpace(q.Status))
        {
            statuses = q.Status.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(s => s.ToLowerInvariant()).ToList();
            if (statuses.Any(s => !PreOrderStatuses.All.Contains(s)))
            {
                return (null, "Unknown status.");
            }
        }

        var visibility = await _c.VisibilityAsync(tenantId, ct);
        IReadOnlyCollection<Guid>? reps = null;
        if (!visibility.All)
        {
            var scope = (visibility.TeamReps ?? new HashSet<string>()).Select(r => Guid.TryParse(r, out var g) ? g : Guid.Empty)
                .Where(g => g != Guid.Empty).ToHashSet();
            if (visibility.CallerId is { } me)
            {
                scope.Add(me);
            }

            reps = q.RepUserId is { } rep ? (scope.Contains(rep) ? new[] { rep } : Array.Empty<Guid>()) : scope;
        }
        else if (q.RepUserId is { } rep)
        {
            reps = new[] { rep };
        }

        var from = q.From is { } f ? new DateTimeOffset(f.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) : (DateTimeOffset?)null;
        var to = q.To is { } t ? new DateTimeOffset(t.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) : (DateTimeOffset?)null;
        return (new PreOrderFilter(statuses, from, to, reps, visibility.All || q.RepUserId is not null ? null : visibility.CallerId, q.PharmacyAccountId,
            q.DepotAccountId, q.SkuId, q.LegalEntityId, q.CountryCode?.Trim().ToUpperInvariant(), q.AwaitingMe ? visibility.CallerId ?? Guid.Empty : null,
            PreOrderResults.Clean(q.Search)), null);
    }

    public async Task<Response<PreOrderDto>> Handle(GetPreOrderByIdQuery request, CancellationToken ct)
    {
        if (_c.Tenant.TenantId is not { } tenantId)
        {
            return PreOrderResults.Invalid<PreOrderDto>("Tenant context is required.");
        }

        var (order, visibility) = await _c.LoadVisibleAsync(tenantId, request.Id, ct);
        if (order is null)
        {
            return PreOrderResults.NotFound<PreOrderDto>();
        }

        order = await _approval.ReconcileAsync(order, visibility, ct);
        return Response<PreOrderDto>.Success(_c.Dto(order, visibility));
    }

    public async Task<Response<PreOrderContextDto>> Handle(GetPreOrderContextQuery request, CancellationToken ct)
    {
        if (_c.Tenant.TenantId is not { } tenantId)
        {
            return PreOrderResults.Invalid<PreOrderContextDto>("Tenant context is required.");
        }

        var (seller, error) = await _c.SellerAsync<PreOrderContextDto>(tenantId, ct);
        if (error is not null)
        {
            return error;
        }

        var settings = await _c.SettingsAsync(tenantId, ct);
        var le = seller!.LegalEntity;
        var tier = PreOrderContext.TierFor(settings, le.CountryCode);
        var mode = await _pricing.ModeAsync(tenantId, le, _c.Today, tier, ct);
        var managers = await _approval.ApproversOfAsync(_c.CallerId!.Value, ct);
        return Response<PreOrderContextDto>.Success(new PreOrderContextDto(le.Id, le.Code, le.Name, seller.Prefix, le.CountryCode.ToUpperInvariant(),
            le.BaseCurrencyCode.ToUpperInvariant(), mode.Mode == PricingModes.Priced ? PreOrderPriceModes.Priced : PreOrderPriceModes.Quantity,
            mode.PriceList?.Currency, mode.PriceList, tier, settings.ApprovalMode,
            new PreOrderApproverPreview(managers.Available, managers.Resolution, managers.PositionCode, managers.PositionName, managers.Managers)));
    }

    public async Task<Response<PreOrderPreviewDto>> Handle(PreviewPreOrderQuery request, CancellationToken ct)
    {
        if (_c.Tenant.TenantId is not { } tenantId)
        {
            return PreOrderResults.Invalid<PreOrderPreviewDto>("Tenant context is required.");
        }

        var lines = request.Input?.Lines ?? Array.Empty<PreOrderLineInput>();
        if (PreOrderValidator.CheckLines(lines) is { } problem)
        {
            return PreOrderResults.Fail<PreOrderPreviewDto>(problem.Code, problem.Message, 400);
        }

        var (seller, error) = await _c.SellerAsync<PreOrderPreviewDto>(tenantId, ct);
        if (error is not null)
        {
            return error;
        }

        var (built, linesError) = await _parts.LinesAsync<PreOrderPreviewDto>(lines, ct);
        if (linesError is not null)
        {
            return linesError;
        }

        var settings = await _c.SettingsAsync(tenantId, ct);
        var le = seller!.LegalEntity;
        var tier = PreOrderContext.TierFor(settings, le.CountryCode);
        var result = await _pricing.EvaluateAsync(tenantId, le.Id, le.CountryCode, _c.Today, tier, built!, ct);
        if (result.Mode != PricingModes.Priced && built!.Any(l => l.DiscountPct is not null))
        {
            return PreOrderResults.Fail<PreOrderPreviewDto>(PreOrderErrors.DiscountNotAllowed, "No price list is in force: a discount cannot be entered.", 400);
        }

        var reasons = new List<string>();
        if (settings.ApprovalMode != PreOrderApprovalModes.AutoWithinLimits)
        {
            reasons.Add(PreOrderApprovalReasons.TenantSetting);
        }

        if (result.Outcome == PricingOutcomes.Soft)
        {
            reasons.Add(PreOrderApprovalReasons.SoftExceeded);
        }

        var fx = await _pricing.FxAsync(tenantId, le.Id, result.Mode == PricingModes.Priced ? result.Currency : null, le.BaseCurrencyCode.ToUpperInvariant(),
            result.Totals?.Gross, _c.Today, ct);
        return Response<PreOrderPreviewDto>.Success(new PreOrderPreviewDto(result.Mode, result.Currency, result.PriceList, tier, result.Outcome,
            result.Lines.Select(l => new PreOrderPreviewLineDto(l.LineNo, l.SkuId, l.Outcome, l.UnitPrice, l.VatRate, l.NetAmount, l.VatAmount, l.FreeQuantity,
                l.MfRatioPct, l.CombinedDiscountPct, l.Findings.Select(f => PreOrderMapping.ToDto(PreOrderPricing.Finding(f))).ToList(), l.Rule?.RuleKey,
                l.Rule?.RuleVersion)).ToList(),
            result.Totals, fx, reasons.Count > 0, reasons, result.RuleSetFingerprint));
    }

    public async Task<Response<PreOrderPharmacyHistoryDto>> Handle(GetPharmacyOrderHistoryQuery request, CancellationToken ct)
    {
        if (_c.Tenant.TenantId is not { } tenantId)
        {
            return PreOrderResults.Invalid<PreOrderPharmacyHistoryDto>("Tenant context is required.");
        }

        var (order, visibility) = await _c.LoadVisibleAsync(tenantId, request.PreOrderId, ct);
        if (order is null || (!visibility.All && !visibility.IsApprover(order)))
        {
            return PreOrderResults.NotFound<PreOrderPharmacyHistoryDto>();
        }

        var from = _c.Now.AddMonths(-PreOrderLimits.PharmacyHistoryMonths);
        var (items, _) = await _c.Orders.ListAsync(tenantId, new PreOrderFilter(CreatedFrom: from, PharmacyAccountId: order.Pharmacy.AccountId), 0,
            PreOrderLimits.PharmacyHistoryMax, ct);
        var submitted = items.Where(o => o.SubmissionNo > 0).ToList(); // submitted orders only (a draft / a discarded draft is not history)
        return Response<PreOrderPharmacyHistoryDto>.Success(new PreOrderPharmacyHistoryDto(order.Pharmacy.AccountId, from,
            submitted.Select(PreOrderPharmacyHistory.Item).ToList(), PreOrderPharmacyHistory.MonthsOf(submitted, _c.Now)));
    }

    public async Task<Response<PreOrderSettingsDto>> Handle(GetPreOrderSettingsQuery request, CancellationToken ct)
    {
        if (_c.Tenant.TenantId is not { } tenantId)
        {
            return PreOrderResults.Invalid<PreOrderSettingsDto>("Tenant context is required.");
        }

        var stored = await _c.Settings.GetAsync(tenantId, ct);
        return Response<PreOrderSettingsDto>.Success(PreOrderMapping.ToDto(stored ?? new PreOrderSettings { TenantId = tenantId }, stored is null));
    }

    public async Task<Response<PreOrderApprovalLinkDto>> Handle(ResolvePreOrderApprovalLinkQuery request, CancellationToken ct)
    {
        if (_c.Tenant.TenantId is not { } tenantId || _c.CallerId is not { } me)
        {
            return PreOrderResults.NotFound<PreOrderApprovalLinkDto>();
        }

        var (link, state) = await _approval.LinkStateAsync(tenantId, request.Token, null, ct);
        if (link is null || !link.ApproverUserIds.Contains(me))
        {
            return PreOrderResults.NotFound<PreOrderApprovalLinkDto>(); // §4.4: never says whose link it is
        }

        PreOrderDto? dto = null;
        if (state == PreOrderApprovalLinkStates.Valid)
        {
            var (order, visibility) = await _c.LoadVisibleAsync(tenantId, link.PreOrderId, ct);
            dto = order is null ? null : _c.Dto(order, visibility);
        }

        return Response<PreOrderApprovalLinkDto>.Success(new PreOrderApprovalLinkDto(state, link.ExpiresAt, link.PreOrderId, dto));
    }

    public async Task<Response<PreOrderExportDto>> Handle(GetPreOrderExportQuery request, CancellationToken ct)
    {
        if (_c.Tenant.TenantId is not { } tenantId)
        {
            return PreOrderResults.Invalid<PreOrderExportDto>("Tenant context is required.");
        }

        var (filter, problem) = await FilterAsync(tenantId, request.Filter, ct);
        if (problem is not null)
        {
            return PreOrderResults.Invalid<PreOrderExportDto>(problem);
        }

        var (items, _) = await _c.Orders.ListAsync(tenantId, filter!, 0, PreOrderLimits.ExportMax, ct);
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("PreOrders");
        string[] header = ["Code", "CreatedAt", "Status", "Pharmacy", "Depot", "Rep", "Lines", "PriceMode", "Currency", "Gross", "LocalCurrency",
            "LocalGross", "EffectiveDiscountPct", "Outcome", "DepotResponse"];
        for (var i = 0; i < header.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = header[i];
        }

        var row = 2;
        foreach (var o in items.Select(o => PreOrderMapping.ToRow(o, _c.CallerId)))
        {
            object?[] cells = [o.Code, o.CreatedAt.UtcDateTime, o.Status, o.PharmacyName, o.DepotName, o.RepName, o.LineCount, o.PriceMode, o.Currency, o.Gross,
                o.LocalCurrencyCode, o.LocalGross, o.EffectiveDiscountPct, o.Outcome, o.DepotResponseStatus];
            for (var i = 0; i < cells.Length; i++)
            {
                sheet.Cell(row, i + 1).Value = XLCellValue.FromObject(cells[i]);
            }

            row++;
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return Response<PreOrderExportDto>.Success(new PreOrderExportDto($"pre-orders-{_c.Today:yyyyMMdd}.xlsx", stream.ToArray()));
    }
}
