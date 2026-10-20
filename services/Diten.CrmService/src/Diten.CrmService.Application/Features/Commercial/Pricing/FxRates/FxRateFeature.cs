using Diten.BuildingBlocks.Auditing;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Domain.Entities.Commercial;
using Diten.CrmService.Domain.Repositories.Commercial;
using MediatR;

namespace Diten.CrmService.Application.Features.Commercial.Pricing.FxRates;

// WP-VW-W6 W6-BE-b — MOD-0156 §4.5 (K-W10 g, D14): the interim FX table. Sales operations write it directly (no four eyes);
// one rate per (legal entity, from, to, date); a correction rewrites the row, the old value goes to the change log; an order
// keeps its own snapshot (§2.3). Every write audited.

public sealed record FxRateInput(Guid? LegalEntityId, string? FromCurrency, string? ToCurrency, DateOnly? RateDate, decimal? Rate, string? Source,
    int? ExpectedVersion = null);

public sealed record FxRateImportInput(IReadOnlyList<FxRateInput>? Rows);

public sealed record FxRateDto(Guid Id, Guid LegalEntityId, string FromCurrency, string ToCurrency, DateOnly RateDate, decimal Rate, string Source,
    string? CreatedBy, DateTimeOffset CreatedAt, string? UpdatedBy, DateTimeOffset? UpdatedAt, int Version);

public sealed record FxRatePageDto(IReadOnlyList<FxRateDto> Items, long Total, int Page, int PageSize);

public sealed record FxRateImportResultDto(int Created, int Updated, int Unchanged, DateOnly? FromDate, DateOnly? ToDate);

public sealed record CreateFxRateCommand(FxRateInput Input) : IRequest<Response<FxRateDto>>, IAuditableCommand, IAuditDetailsProvider
{
    public AuditDescriptor DescribeAudit() => PricingAudit.Describe(AuditOperation.Create, PricingAudit.FxRateEntity, null);

    public void DescribeAuditDetails(AuditDetails details, object? response)
    {
        var dto = (response as Response<FxRateDto>)?.Data;
        details.EntityId = dto?.Id;
        details.ObjectVersion = dto?.Version;
        details.Set("legalEntityId", Input?.LegalEntityId).Set("pair", $"{Input?.FromCurrency}/{Input?.ToCurrency}")
            .Set("rateDate", Input?.RateDate?.ToString("yyyy-MM-dd")).Set("rate", Input?.Rate).Set("source", Input?.Source);
    }
}

public sealed record UpdateFxRateCommand(Guid Id, FxRateInput Input) : IRequest<Response<FxRateDto>>, IAuditableCommand, IAuditDetailsProvider
{
    public AuditDescriptor DescribeAudit() => PricingAudit.Describe(AuditOperation.Update, PricingAudit.FxRateEntity, Id);

    public void DescribeAuditDetails(AuditDetails details, object? response)
    {
        details.ObjectVersion = (response as Response<FxRateDto>)?.Data?.Version;
        details.Set("rate", Input?.Rate).Set("source", Input?.Source);
    }
}

public sealed record ImportFxRatesCommand(FxRateImportInput Input)
    : IRequest<Response<FxRateImportResultDto>>, IAuditableCommand, IAuditDetailsProvider
{
    public AuditDescriptor DescribeAudit() => PricingAudit.Describe(AuditOperation.Create, PricingAudit.FxRateEntity, null);

    public void DescribeAuditDetails(AuditDetails details, object? response)
    {
        var dto = (response as Response<FxRateImportResultDto>)?.Data;
        details.Set("rows", Input?.Rows?.Count ?? 0).Set("created", dto?.Created).Set("updated", dto?.Updated)
            .Set("from", dto?.FromDate?.ToString("yyyy-MM-dd")).Set("to", dto?.ToDate?.ToString("yyyy-MM-dd"));
    }
}

public sealed record GetFxRateListQuery(Guid? LegalEntityId, string? FromCurrency, string? ToCurrency, DateOnly? From, DateOnly? To, int? Page, int? PageSize)
    : IRequest<Response<FxRatePageDto>>;

public static class FxRateValidator
{
    public const int MaxImportRows = 1000;

    public static (string Code, string Message)? Check(FxRateInput? input, bool create)
    {
        if (input is null)
        {
            return (PricingErrors.FxRateInvalid, "A body is required.");
        }

        if (create)
        {
            if (input.LegalEntityId is null || input.LegalEntityId == Guid.Empty)
            {
                return (PricingErrors.LegalEntityInvalid, "The legal entity is required.");
            }

            var from = PricingCommon.NormalizeCurrency(input.FromCurrency);
            var to = PricingCommon.NormalizeCurrency(input.ToCurrency);
            if (!PricingCommon.IsCurrency(from) || !PricingCommon.IsCurrency(to) || from == to)
            {
                return (PricingErrors.FxRateInvalid, "The pair is two different ISO 4217 currencies.");
            }

            if (input.RateDate is null)
            {
                return (PricingErrors.FxRateInvalid, "The rate date is required.");
            }
        }

        if ((create || input.Rate is not null) && (input.Rate is not { } rate || rate <= 0m || PricingCommon.Decimals(rate) > PricingLimits.FxDecimals))
        {
            return (PricingErrors.FxRateInvalid, $"The rate is above 0 with at most {PricingLimits.FxDecimals} decimals.");
        }

        var source = PricingCommon.Clean(input.Source);
        if ((create || input.Source is not null) && (source is null || source.Length > PricingLimits.FxSourceMax))
        {
            return (PricingErrors.FxRateInvalid, $"The source is required (≤ {PricingLimits.FxSourceMax} characters, e.g. \"manual\").");
        }

        return null;
    }

    public static FxRateDto ToDto(FxRate r)
        => new(r.Id, r.LegalEntityId, r.FromCurrency, r.ToCurrency, r.RateDate, r.Rate, r.Source, r.CreatedBy, r.CreatedAt, r.UpdatedBy, r.UpdatedAt, r.Version);
}

public sealed class FxRateHandlers :
    IRequestHandler<CreateFxRateCommand, Response<FxRateDto>>,
    IRequestHandler<UpdateFxRateCommand, Response<FxRateDto>>,
    IRequestHandler<ImportFxRatesCommand, Response<FxRateImportResultDto>>,
    IRequestHandler<GetFxRateListQuery, Response<FxRatePageDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IFxRateRepository _rates;
    private readonly IPricingLegalEntityDirectory _legalEntities;
    private readonly PricingChangeLogWriter _log;

    public FxRateHandlers(ITenantContext tenant, IActorContext actor, IFxRateRepository rates, IPricingLegalEntityDirectory legalEntities,
        PricingChangeLogWriter log)
    {
        _tenant = tenant;
        _actor = actor;
        _rates = rates;
        _legalEntities = legalEntities;
        _log = log;
    }

    public async Task<Response<FxRateDto>> Handle(CreateFxRateCommand request, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return PricingCommon.Invalid<FxRateDto>("Tenant context is required.");
        }

        if (FxRateValidator.Check(request.Input, create: true) is { } problem)
        {
            return PricingCommon.Fail<FxRateDto>(problem.Code, problem.Message, 400);
        }

        if (await LegalEntityProblemAsync<FxRateDto>(request.Input.LegalEntityId!.Value, ct) is { } refused)
        {
            return refused;
        }

        var rate = New(tenantId, request.Input);
        if (!await _rates.TryInsertAsync(rate, ct))
        {
            return PricingCommon.Fail<FxRateDto>(PricingErrors.FxRateDuplicate,
                "A rate of this company, pair and day already exists; correct it instead.", 409);
        }

        await _log.WriteAsync(tenantId, PricingChangeEntityTypes.FxRate, rate.Id, rate.Version, rate.LegalEntityId, PricingChangeActions.Created, rate.CreatedBy,
            PricingChangeLogWriter.Created(("pair", $"{rate.FromCurrency}/{rate.ToCurrency}"), ("rateDate", rate.RateDate), ("rate", rate.Rate),
                ("source", rate.Source)), ct);
        return Response<FxRateDto>.Success(FxRateValidator.ToDto(rate), 201);
    }

    public async Task<Response<FxRateDto>> Handle(UpdateFxRateCommand request, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return PricingCommon.Invalid<FxRateDto>("Tenant context is required.");
        }

        if (FxRateValidator.Check(request.Input, create: false) is { } problem)
        {
            return PricingCommon.Fail<FxRateDto>(problem.Code, problem.Message, 400);
        }

        var rate = await _rates.GetByIdAsync(tenantId, request.Id, ct);
        if (rate is null || rate.TenantId != tenantId || rate.IsDeleted)
        {
            return PricingCommon.NotFound<FxRateDto>();
        }

        if (request.Input.ExpectedVersion is { } expected && expected != rate.Version)
        {
            return PricingCommon.Stale<FxRateDto>();
        }

        return await CorrectAsync(rate, request.Input.Rate ?? rate.Rate, PricingCommon.Clean(request.Input.Source) ?? rate.Source, ct) is { } result
            ? Response<FxRateDto>.Success(FxRateValidator.ToDto(result))
            : PricingCommon.Stale<FxRateDto>();
    }

    public async Task<Response<FxRateImportResultDto>> Handle(ImportFxRatesCommand request, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return PricingCommon.Invalid<FxRateImportResultDto>("Tenant context is required.");
        }

        var rows = request.Input?.Rows;
        if (rows is not { Count: > 0 } || rows.Count > FxRateValidator.MaxImportRows)
        {
            return PricingCommon.Fail<FxRateImportResultDto>(PricingErrors.FxRateInvalid, $"1–{FxRateValidator.MaxImportRows} rows.", 400);
        }

        for (var i = 0; i < rows.Count; i++)
        {
            if (FxRateValidator.Check(rows[i], create: true) is { } problem)
            {
                return PricingCommon.Fail<FxRateImportResultDto>(problem.Code, $"Row {i + 1}: {problem.Message}", 400);
            }
        }

        foreach (var legalEntityId in rows.Select(r => r.LegalEntityId!.Value).Distinct())
        {
            if (await LegalEntityProblemAsync<FxRateImportResultDto>(legalEntityId, ct) is { } refused)
            {
                return refused;
            }
        }

        // the last row of a key wins inside the file
        var byKey = rows.GroupBy(r => (r.LegalEntityId!.Value, PricingCommon.NormalizeCurrency(r.FromCurrency), PricingCommon.NormalizeCurrency(r.ToCurrency),
            r.RateDate!.Value)).Select(g => g.Last()).ToList();
        int created = 0, updated = 0, unchanged = 0;
        foreach (var row in byKey)
        {
            var existing = await _rates.GetByKeyAsync(tenantId, row.LegalEntityId!.Value, PricingCommon.NormalizeCurrency(row.FromCurrency),
                PricingCommon.NormalizeCurrency(row.ToCurrency), row.RateDate!.Value, ct);
            if (existing is null)
            {
                var rate = New(tenantId, row);
                if (await _rates.TryInsertAsync(rate, ct))
                {
                    created++;
                    await _log.WriteAsync(tenantId, PricingChangeEntityTypes.FxRate, rate.Id, rate.Version, rate.LegalEntityId, PricingChangeActions.Imported,
                        rate.CreatedBy, PricingChangeLogWriter.Created(("pair", $"{rate.FromCurrency}/{rate.ToCurrency}"), ("rateDate", rate.RateDate),
                            ("rate", rate.Rate), ("source", rate.Source)), ct);
                    continue;
                }

                existing = await _rates.GetByKeyAsync(tenantId, row.LegalEntityId!.Value, PricingCommon.NormalizeCurrency(row.FromCurrency),
                    PricingCommon.NormalizeCurrency(row.ToCurrency), row.RateDate!.Value, ct);
                if (existing is null)
                {
                    return PricingCommon.Stale<FxRateImportResultDto>();
                }
            }

            if (existing.Rate == row.Rate && existing.Source == PricingCommon.Clean(row.Source))
            {
                unchanged++;
                continue;
            }

            if (await CorrectAsync(existing, row.Rate!.Value, PricingCommon.Clean(row.Source)!, ct) is null)
            {
                return PricingCommon.Stale<FxRateImportResultDto>();
            }

            updated++;
        }

        return Response<FxRateImportResultDto>.Success(new FxRateImportResultDto(created, updated, unchanged,
            byKey.Min(r => r.RateDate), byKey.Max(r => r.RateDate)));
    }

    public async Task<Response<FxRatePageDto>> Handle(GetFxRateListQuery request, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return PricingCommon.Invalid<FxRatePageDto>("Tenant context is required.");
        }

        var (page, size) = PriceLists.Handlers.QueryHandlers.PriceListQueryHandlers.Paging(request.Page, request.PageSize);
        var (items, total) = await _rates.ListAsync(tenantId, new FxRateFilter(request.LegalEntityId,
            request.FromCurrency is null ? null : PricingCommon.NormalizeCurrency(request.FromCurrency),
            request.ToCurrency is null ? null : PricingCommon.NormalizeCurrency(request.ToCurrency), request.From, request.To), (page - 1) * size, size, ct);
        return Response<FxRatePageDto>.Success(new FxRatePageDto(items.Where(r => r.TenantId == tenantId).Select(FxRateValidator.ToDto).ToList(),
            total, page, size));
    }

    private FxRate New(Guid tenantId, FxRateInput input) => new()
    {
        TenantId = tenantId, LegalEntityId = input.LegalEntityId!.Value, FromCurrency = PricingCommon.NormalizeCurrency(input.FromCurrency),
        ToCurrency = PricingCommon.NormalizeCurrency(input.ToCurrency), RateDate = input.RateDate!.Value, Rate = input.Rate!.Value,
        Source = PricingCommon.Clean(input.Source)!, CreatedBy = _actor.ActorName, UpdatedBy = _actor.ActorName
    };

    /// <summary>A correction: the row changes, the old value goes to the change log (orders keep their own snapshot).</summary>
    private async Task<FxRate?> CorrectAsync(FxRate rate, decimal newRate, string newSource, CancellationToken ct)
    {
        var changes = PricingChangeLogWriter.Diff(("rate", rate.Rate, newRate), ("source", rate.Source, newSource));
        if (changes.Count == 0)
        {
            return rate;
        }

        rate.Rate = newRate;
        rate.Source = newSource;
        rate.UpdatedBy = _actor.ActorName;
        rate.UpdatedAt = DateTimeOffset.UtcNow;
        if (!await _rates.ReplaceAsync(rate, rate.Version, ct))
        {
            return null;
        }

        await _log.WriteAsync(rate.TenantId, PricingChangeEntityTypes.FxRate, rate.Id, rate.Version, rate.LegalEntityId, PricingChangeActions.Updated,
            rate.UpdatedBy, changes, ct);
        return rate;
    }

    private async Task<Response<T>?> LegalEntityProblemAsync<T>(Guid legalEntityId, CancellationToken ct)
    {
        var lookup = await _legalEntities.GetAsync(legalEntityId, ct);
        return lookup.Outcome switch
        {
            PricingLookupOutcome.Unavailable => PricingCommon.MdmUnavailable<T>(),
            PricingLookupOutcome.NotFound => PricingCommon.Fail<T>(PricingErrors.LegalEntityInvalid, "The legal entity does not exist in this tenant.", 400),
            _ => null
        };
    }
}
