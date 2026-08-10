using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Application.Features.Lookups.Services;
using Diten.Platform.Application.Features.Tasks.Queries;
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
/// </summary>
public sealed class GetTaskFieldDefinitionOptionsHandler
    : IRequestHandler<GetTaskFieldDefinitionOptionsQuery, Response<IReadOnlyList<TaskFieldOptionDto>>>
{
    private readonly ITaskFieldDefinitionRepository _definitions;
    private readonly IPlatformLookupProvider _lookups;
    private readonly IBusinessReferenceDataConsumerQueryService _referenceData;
    private readonly ITenantContext _tenantContext;
    private readonly IConfiguration _configuration;

    public GetTaskFieldDefinitionOptionsHandler(
        ITaskFieldDefinitionRepository definitions,
        IPlatformLookupProvider lookups,
        IBusinessReferenceDataConsumerQueryService referenceData,
        ITenantContext tenantContext,
        IConfiguration configuration)
    {
        _definitions = definitions;
        _lookups = lookups;
        _referenceData = referenceData;
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

        if (definition.OptionsSourceKind == TaskFieldOptionsSourceKind.None
            || string.IsNullOrWhiteSpace(definition.OptionsSourceKey))
        {
            return Fail(
                $"Field '{definition.Code}' declares no option source.", 400,
                TaskReasonCodes.FieldOptionsUnresolved, request.CorrelationId);
        }

        var key = definition.OptionsSourceKey.Trim();

        var options = definition.OptionsSourceKind switch
        {
            TaskFieldOptionsSourceKind.PlatformLookup => await ResolvePlatformLookupAsync(key, ct),
            TaskFieldOptionsSourceKind.BusinessReferenceData => await ResolveReferenceDataAsync(key, ct),
            _ => null
        };

        if (options is null)
        {
            return Fail(
                $"The option source '{definition.OptionsSourceKind}/{key}' named by field '{definition.Code}' "
                + "could not be resolved.",
                404, TaskReasonCodes.FieldOptionsUnresolved, request.CorrelationId);
        }

        return Response<IReadOnlyList<TaskFieldOptionDto>>.Success(options, correlationId: request.CorrelationId);
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
