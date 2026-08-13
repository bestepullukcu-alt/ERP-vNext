using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Application.Features.Lookups;
using Diten.Platform.Application.Features.Lookups.Services;
using Diten.Platform.Application.Features.Tasks.Queries;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Configuration;

namespace Diten.Platform.Application.Features.Tasks.Handlers.QueryHandlers;

/// <summary>Phase 5 — reading the configurable field catalogue.</summary>
public sealed class GetTaskFieldDefinitionListHandler
    : IRequestHandler<GetTaskFieldDefinitionListQuery, Response<IReadOnlyList<TaskFieldDefinitionDto>>>
{
    private readonly ITaskFieldDefinitionRepository _definitions;

    public GetTaskFieldDefinitionListHandler(ITaskFieldDefinitionRepository definitions)
        => _definitions = definitions;

    public async Task<Response<IReadOnlyList<TaskFieldDefinitionDto>>> Handle(
        GetTaskFieldDefinitionListQuery request, CancellationToken ct)
    {
        // Retired definitions are not offered for management, but PAUSED ones are: a definition that vanished
        // when it was switched off could never be switched back on.
        IReadOnlyList<TaskFieldDefinitionDto> result = (await _definitions.ListAllAsync(ct))
            .Where(definition => definition.DeletedAt is null)
            .Select(TaskFieldDefinitionMapper.ToDto)
            .ToList();

        return Response<IReadOnlyList<TaskFieldDefinitionDto>>.Success(result, correlationId: request.CorrelationId);
    }
}

public sealed class GetTaskFieldDefinitionByIdHandler
    : IRequestHandler<GetTaskFieldDefinitionByIdQuery, Response<TaskFieldDefinitionDto>>
{
    private readonly ITaskFieldDefinitionRepository _definitions;

    public GetTaskFieldDefinitionByIdHandler(ITaskFieldDefinitionRepository definitions)
        => _definitions = definitions;

    public async Task<Response<TaskFieldDefinitionDto>> Handle(
        GetTaskFieldDefinitionByIdQuery request, CancellationToken ct)
    {
        // Tenant-scoped repository: another tenant's definition does not resolve, so the caller learns nothing
        // about its existence.
        var definition = await _definitions.GetByIdAsync(request.Id, ct);
        if (definition is null || definition.DeletedAt is not null)
        {
            return Response<TaskFieldDefinitionDto>.Fail(
                "Field definition not found.", 404,
                TaskReasonCodes.FieldDefinitionNotFound, request.CorrelationId);
        }

        return Response<TaskFieldDefinitionDto>.Success(
            TaskFieldDefinitionMapper.ToDto(definition), correlationId: request.CorrelationId);
    }
}

/// <summary>
/// Phase 5 — resolving ONE field's option list from the source its definition names.
///
/// <para>This exists because the browser must never name the source. Before it, a form could only have filled an
/// option-driven field by calling the reference-data read directly, which is allow-listed to three global sets
/// and 403/404s everything else — so a tenant field pointing at its own set had an unfillable picker. Here the
/// DEFINITION is the allow-list: a set or lookup key is readable exactly because a live field definition names
/// it, and for no other reason.</para>
///
/// <para>An unresolvable source is REPORTED, never answered with an empty list: the form drops a field it cannot
/// fill, and it can only make that decision if "no options" and "no such source" arrive differently.</para>
///
/// <para><b>All THREE source kinds land here, and none of them has a private path.</b> A module record source is
/// the awkward one — it can hold thousands of rows, so it is searched rather than enumerated — but the search
/// term and the cap live on the shared query and the short sources apply them to the list they already had. The
/// alternative, a second endpoint for records, is how the second source stops obeying the contract and the third
/// one rewrites it.</para>
/// </summary>
public sealed class GetTaskFieldDefinitionOptionsHandler
    : IRequestHandler<GetTaskFieldDefinitionOptionsQuery, Response<IReadOnlyList<TaskFieldOptionDto>>>
{
    private readonly ITaskFieldDefinitionRepository _definitions;
    private readonly IPlatformLookupProvider _lookups;
    private readonly IBusinessReferenceDataConsumerQueryService _referenceData;
    private readonly ITaskRecordSourceRegistry _recordSources;
    private readonly ITenantContext _tenantContext;
    private readonly IConfiguration _configuration;

    /// <summary>BL-024 Phase 2 — who is asking for this field's option list.</summary>
    private readonly IActorPermissionContext _actor;

    public GetTaskFieldDefinitionOptionsHandler(
        ITaskFieldDefinitionRepository definitions,
        IPlatformLookupProvider lookups,
        IBusinessReferenceDataConsumerQueryService referenceData,
        ITaskRecordSourceRegistry recordSources,
        ITenantContext tenantContext,
        IConfiguration configuration,
        IActorPermissionContext actor)
    {
        _actor = actor;
        _definitions = definitions;
        _lookups = lookups;
        _referenceData = referenceData;
        _recordSources = recordSources;
        _tenantContext = tenantContext;
        _configuration = configuration;
    }

    public async Task<Response<IReadOnlyList<TaskFieldOptionDto>>> Handle(
        GetTaskFieldDefinitionOptionsQuery request, CancellationToken ct)
    {
        var definition = (await _definitions.ListActiveAsync(ct))
            .FirstOrDefault(d => string.Equals(d.Code, request.Code, StringComparison.OrdinalIgnoreCase));

        if (definition is null)
        {
            // Tenant-scoped repository: another tenant's definition does not resolve, so the caller learns
            // nothing about its existence.
            return Fail(
                $"Unknown task field definition '{request.Code}'.", 404,
                TaskReasonCodes.FieldDefinitionUnknown, request.CorrelationId);
        }

        /*
         * BL-024 Phase 2 — A HIDDEN FIELD'S PICKER IS HIDDEN TOO.
         *
         * This route and the `records` route beside it sit on `platform.tasks.read`, deliberately: filling a
         * field you were asked to fill is an ordinary task read. But that reasoning only holds for a field the
         * caller may SEE. Redacting a value while leaving its selector open is redaction in name only — the list
         * the field was hidden to protect stays fully enumerable, one route over. BL-024's own note raised this,
         * and it is the reason the endpoint is in scope rather than a later tidy-up.
         *
         * 403, not 404. The definition's EXISTENCE is not the secret: the catalogue is readable through
         * `GET field-definitions`, so pretending it is missing would be a lie the caller can disprove in one
         * request, and lies in error codes are how people learn to distrust the error codes that matter.
         */
        if (!TaskFieldAccessRules.CanReadOptions(definition, _actor))
        {
            return Fail(
                $"You are not permitted to read the options of field '{definition.Code}'.", 403,
                TaskReasonCodes.FieldAccessDenied, request.CorrelationId);
        }

        if (definition.OptionsSourceKind == TaskFieldOptionsSourceKind.None
            || string.IsNullOrWhiteSpace(definition.OptionsSourceKey))
        {
            return Fail(
                $"Field '{definition.Code}' declares no option source.", 400,
                TaskReasonCodes.FieldOptionsUnresolved, request.CorrelationId);
        }

        var key = definition.OptionsSourceKey.Trim();
        var take = TaskRecordSearchLimits.Clamp(request.Take);
        var ids = request.Ids?.Where(id => !string.IsNullOrWhiteSpace(id)).ToList();

        var options = definition.OptionsSourceKind switch
        {
            TaskFieldOptionsSourceKind.PlatformLookup => await ResolvePlatformLookupAsync(key, ct),
            TaskFieldOptionsSourceKind.BusinessReferenceData => await ResolveReferenceDataAsync(key, ct),
            TaskFieldOptionsSourceKind.ModuleRecord => await ResolveModuleRecordsAsync(key, request.Term, ids, take, ct),
            _ => null
        };

        if (options is null)
        {
            return Fail(
                $"The option source '{definition.OptionsSourceKind}/{key}' named by field '{definition.Code}' "
                + "could not be resolved.",
                404, TaskReasonCodes.FieldOptionsUnresolved, request.CorrelationId);
        }

        /*
         * Term and id filtering applied ONCE, after resolution, so the two short sources get searching for free
         * and never grow their own copy of it. A record source has already applied both — it has to, because
         * pulling five thousand rows back to filter them here is the thing the cap exists to prevent — and
         * re-applying them to what it returned changes nothing.
         */
        return Response<IReadOnlyList<TaskFieldOptionDto>>.Success(
            Narrow(options, request.Term, ids, take), correlationId: request.CorrelationId);
    }

    /// <summary>
    /// The shared narrowing every source's result passes through: hydration by identity when ids were named,
    /// otherwise a term filter and the cap.
    /// </summary>
    private static IReadOnlyList<TaskFieldOptionDto> Narrow(
        IReadOnlyList<TaskFieldOptionDto> options, string? term, IReadOnlyList<string>? ids, int take)
    {
        if (ids is { Count: > 0 })
        {
            // A hydration is NOT capped: every stored value must come back, or the edit form silently drops the
            // ones past the limit and posts a task with fields it never showed.
            var wanted = ids.ToHashSet(StringComparer.OrdinalIgnoreCase);
            return options.Where(option => wanted.Contains(option.Value)).ToList();
        }

        if (!string.IsNullOrWhiteSpace(term))
        {
            var needle = term.Trim();
            options = options
                .Where(option =>
                    option.Label.Contains(needle, StringComparison.CurrentCultureIgnoreCase)
                    || option.Value.Contains(needle, StringComparison.CurrentCultureIgnoreCase)
                    || (option.Secondary?.Contains(needle, StringComparison.CurrentCultureIgnoreCase) ?? false))
                .ToList();
        }

        return options.Count <= take ? options : options.Take(take).ToList();
    }

    /// <summary>
    /// Another module's records, through the registry rather than a switch. An unregistered key returns null and
    /// becomes the ordinary "source could not be resolved" refusal — the same answer a mistyped lookup key gets,
    /// which is why the form needs no new rule to drop the field.
    /// </summary>
    private async Task<IReadOnlyList<TaskFieldOptionDto>?> ResolveModuleRecordsAsync(
        string sourceKey, string? term, IReadOnlyList<string>? ids, int take, CancellationToken ct)
    {
        var source = _recordSources.Find(sourceKey);
        if (source is null)
        {
            return null;
        }

        var records = ids is { Count: > 0 }
            ? await source.ResolveAsync(ids, ct)
            : await source.SearchAsync(term, take, ct);

        // Value = identity, Label = the name, Secondary = the business key and whatever disambiguates it. This
        // is the ONE place a record becomes an option, so no caller ever sees the record shape.
        return records
            .Select(record => new TaskFieldOptionDto(
                record.Id,
                record.Name,
                string.IsNullOrWhiteSpace(record.Secondary) ? record.Code : $"{record.Code} · {record.Secondary}"))
            .ToList();
    }

    private async Task<IReadOnlyList<TaskFieldOptionDto>?> ResolvePlatformLookupAsync(
        string key, CancellationToken ct)
    {
        var options = await _lookups.GetLookupOptionsAsync(key, ct);
        return options?
            .OrderBy(option => option.SortOrder ?? int.MaxValue)
            .ThenBy(option => option.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(option => new TaskFieldOptionDto(option.Value, option.Name))
            .ToList();
    }

    /// <summary>
    /// Published values of a reference set. Read in the CALLER's tenant first, because a tenant's own governed
    /// set is the ordinary case; the seeded universal sets (country, legal-form, base-currency) live under the
    /// reference tenant instead, so a miss retries there.
    ///
    /// <para>The reference tenant id comes from the SAME configuration key the catalog seed writes under, so the
    /// seed and this read stay in lock-step — the identical stopgap <c>TenantReferenceDataController</c> already
    /// runs on, and it disappears the day BRD "Global" scope becomes natively cross-tenant.</para>
    /// </summary>
    private async Task<IReadOnlyList<TaskFieldOptionDto>?> ResolveReferenceDataAsync(
        string setCode, CancellationToken ct)
    {
        var published = await TryReadPublishedAsync(setCode, ct);

        if (published is null
            && Guid.TryParse(_configuration["BusinessReferenceData:CatalogLoad:TenantId"], out var referenceTenantId)
            && referenceTenantId != Guid.Empty
            && referenceTenantId != _tenantContext.TenantId)
        {
            using (TenantScope.Begin(_tenantContext, referenceTenantId))
            {
                published = await TryReadPublishedAsync(setCode, ct);
            }
        }

        return published?.Items
            .Where(item => item.IsActive)
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
            .Select(item => new TaskFieldOptionDto(item.Code, item.Label))
            .ToList();
    }

    private async Task<BusinessReferenceDataPublishedValuesModel?> TryReadPublishedAsync(
        string setCode, CancellationToken ct)
    {
        try
        {
            return await _referenceData.GetPublishedValuesAsync(setCode, null, ct);
        }
        catch (KeyNotFoundException)
        {
            // No such set here. Same two exception types BusinessReferenceDataActiveMembershipService catches.
            return null;
        }
        catch (InvalidOperationException)
        {
            // The set exists but has nothing published — a source that cannot fill a picker either way.
            return null;
        }
    }

    private static Response<IReadOnlyList<TaskFieldOptionDto>> Fail(
        string message, int status, string reasonCode, string correlationId)
        => Response<IReadOnlyList<TaskFieldOptionDto>>.Fail(message, status, reasonCode, correlationId);
}

/// <summary>
/// What an administrator may CHOOSE as a field's option source, for the kind they picked.
///
/// <para>The screen used to take the key as free text, and a typo produced a field that disappeared: the
/// resolver refused the unknown source and the form dropped the field rather than showing an unfillable picker.
/// Both of those behaviours are correct and both stay. What is removed is the way to get there — a key that can
/// only be chosen cannot be mistyped.</para>
/// </summary>
public sealed class GetTaskFieldOptionSourcesHandler
    : IRequestHandler<GetTaskFieldOptionSourcesQuery, Response<IReadOnlyList<TaskFieldOptionSourceDto>>>
{
    /*
     * The platform keys this screen offers. NOT every key PlatformLookupProvider answers: the rest are operator
     * surfaces (audit categories, notification channels, subscription cycles) that belong on a task about as
     * much as a database table name does. Listed rather than reflected — BL-040's lesson is that reflection over
     * a shape nobody re-checks fails silently, and this list is short enough to read.
     */
    private static readonly IReadOnlyList<(string Key, string ResourceKey)> PlatformLookupSources =
    [
        (PlatformLookupKeys.Countries, TaskFieldOptionSourceLabels.KeyFor(PlatformLookupKeys.Countries)),
        (PlatformLookupKeys.Currencies, TaskFieldOptionSourceLabels.KeyFor(PlatformLookupKeys.Currencies)),
        (PlatformLookupKeys.Languages, TaskFieldOptionSourceLabels.KeyFor(PlatformLookupKeys.Languages)),
        (PlatformLookupKeys.Timezones, TaskFieldOptionSourceLabels.KeyFor(PlatformLookupKeys.Timezones))
    ];

    private readonly IBusinessReferenceDataStewardshipRepository _sets;
    private readonly ITaskRecordSourceRegistry _recordSources;
    private readonly ITenantContext _tenantContext;
    private readonly IConfiguration _configuration;

    public GetTaskFieldOptionSourcesHandler(
        IBusinessReferenceDataStewardshipRepository sets,
        ITaskRecordSourceRegistry recordSources,
        ITenantContext tenantContext,
        IConfiguration configuration)
    {
        _sets = sets;
        _recordSources = recordSources;
        _tenantContext = tenantContext;
        _configuration = configuration;
    }

    public async Task<Response<IReadOnlyList<TaskFieldOptionSourceDto>>> Handle(
        GetTaskFieldOptionSourcesQuery request, CancellationToken ct)
    {
        IReadOnlyList<TaskFieldOptionSourceDto> sources = request.Kind switch
        {
            TaskFieldOptionsSourceKind.PlatformLookup => PlatformLookupSources
                .Select(entry => new TaskFieldOptionSourceDto(entry.Key, entry.Key, entry.ResourceKey, null))
                .ToList(),

            TaskFieldOptionsSourceKind.BusinessReferenceData => await ListReferenceSetsAsync(ct),

            // The registry, not a list: a module that registers a source appears here without this file being
            // edited, which is the whole reason the registry exists.
            TaskFieldOptionsSourceKind.ModuleRecord => _recordSources.All
                .Select(source => new TaskFieldOptionSourceDto(
                    source.SourceKey, source.SourceKey, source.LabelResourceKey, source.ModuleCode))
                .ToList(),

            // "None" is an answer, not a failure: the screen asks for every kind including the one with no
            // sources, and an empty list is exactly what it should render.
            _ => []
        };

        return Response<IReadOnlyList<TaskFieldOptionSourceDto>>.Success(
            sources, correlationId: request.CorrelationId);
    }

    /// <summary>
    /// The tenant's own reference sets, PLUS the seeded universal ones that live under the reference tenant —
    /// the same two-step read <see cref="GetTaskFieldDefinitionOptionsHandler"/> performs, because a screen that
    /// cannot offer "country" while the resolver happily resolves it is a screen that teaches the wrong thing.
    /// </summary>
    private async Task<IReadOnlyList<TaskFieldOptionSourceDto>> ListReferenceSetsAsync(CancellationToken ct)
    {
        var byCode = new Dictionary<string, TaskFieldOptionSourceDto>(StringComparer.OrdinalIgnoreCase);

        await CollectAsync(byCode, ct);

        if (Guid.TryParse(_configuration["BusinessReferenceData:CatalogLoad:TenantId"], out var referenceTenantId)
            && referenceTenantId != Guid.Empty
            && referenceTenantId != _tenantContext.TenantId)
        {
            using (TenantScope.Begin(_tenantContext, referenceTenantId))
            {
                await CollectAsync(byCode, ct);
            }
        }

        return byCode.Values.OrderBy(dto => dto.Label, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    private async Task CollectAsync(
        Dictionary<string, TaskFieldOptionSourceDto> byCode, CancellationToken ct)
    {
        var page = await _sets.QuerySetsAsync(
            new BusinessReferenceDataSetListQuery(
                Search: null, Status: null, ScopeType: null,
                Page: 1, PageSize: 200, Sort: "setCode"),
            ct);

        // Only sets that have actually PUBLISHED a version. The resolver reads published values, so a draft set
        // resolves to nothing — offering it here would recreate the vanishing field this endpoint exists to
        // prevent, this time with a key nobody mistyped.
        foreach (var set in page.Items.Where(set =>
                     set.PublishedVersionId is not null
                     && set.Status == Domain.Entities.BusinessReferenceDataSetStatus.Active))
        {
            // A tenant's set carries its OWN name — the tenant's words, not a resource key we could translate.
            byCode.TryAdd(set.SetCode, new TaskFieldOptionSourceDto(set.SetCode, set.Name, null, null));
        }
    }
}

public static class TaskFieldDefinitionMapper
{
    public static TaskFieldDefinitionDto ToDto(TaskFieldDefinition definition) => new(
        definition.Id,
        definition.Code,
        // BOTH label sources cross the wire, and exactly one is populated. The client decides which contract
        // label form to render from which one is present — it never guesses, and it never falls back to the code.
        definition.LabelResourceKey,
        definition.LabelText,
        // Enums as STRINGS, the live Platform convention — an enum reaching a client as a number is a defect this
        // module has already shipped twice.
        definition.ValueType.ToString(),
        definition.Section,
        definition.Importance.ToString(),
        definition.IsRequired,
        definition.SortOrder,
        definition.OptionsSourceKind.ToString(),
        definition.OptionsSourceKey,
        definition.AppliesToModuleCode,
        definition.Classification.ToString(),
        definition.DefaultAccessState.ToString(),
        definition.IsActive,
        definition.Version,
        definition.CreatedAt);
}
