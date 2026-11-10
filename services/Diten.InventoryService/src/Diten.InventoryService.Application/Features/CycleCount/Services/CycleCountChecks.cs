using System.Globalization;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Common.Upstream;
using Diten.InventoryService.Application.Features.CycleCounting.Ports;
using Diten.InventoryService.Application.Features.CycleCounts;
using Diten.InventoryService.Application.Features.Warehouse.Ports;
using Diten.InventoryService.Domain.CycleCounting.Enums;
using Diten.InventoryService.Domain.CycleCounting.Rules;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Repositories;
using Diten.InventoryService.Domain.Warehouse.Rules;

namespace Diten.InventoryService.Application.Features.CycleCounting.Services;

/// <summary>The Draft form after its business checks (pack §12): the parsed type and date plus the normalized lists.</summary>
public sealed record CycleCountForm(Guid WarehouseId, CycleCountType CountType, bool Blind, IReadOnlyList<Guid> ScopeLocationIds, IReadOnlyList<Guid> ScopeSkuIds,
    string PlannedDate, Guid? AssignedTo, string? Note)
{
    /// <summary>The form as fingerprint fields (operation and target are added by the caller).</summary>
    public (string Name, string? Value)[] Fields() =>
    [
        ("warehouseId", WarehouseId.ToString("D")), ("countType", CountType.ToString()), ("blind", Blind ? "true" : "false"),
        ("scopeLocationIds", CycleCountSupport.List(ScopeLocationIds.Select(x => x.ToString("D")))),
        ("scopeSkuIds", CycleCountSupport.List(ScopeSkuIds.Select(x => x.ToString("D")))), ("plannedDate", PlannedDate),
        ("assignedTo", AssignedTo?.ToString("D")), ("note", Note)
    ];
}

/// <summary>
/// The business checks of a Draft form and the record number (pack §4.1, §8.12, §12): the warehouse and the scope nodes on the in-process LOCATION
/// store (company-scoped — another company's node does not exist), the assignee in the user directory, the company prefix from MDM, and the
/// number from MOD-0178's temporary counter with type <c>CNT</c> (Q-7 = B; 0181 writes no counter of its own).
/// </summary>
public sealed class CycleCountChecks
{
    private readonly ILocationRepository _locations;
    private readonly ICycleCountAssigneeDirectory _assignees;
    private readonly ILegalEntityMasterClient _legalEntities;
    private readonly IWarehouseRecordCodeAllocator _numbers;
    private readonly CycleCountSupport _support;

    public CycleCountChecks(ILocationRepository locations, ICycleCountAssigneeDirectory assignees, ILegalEntityMasterClient legalEntities,
        IWarehouseRecordCodeAllocator numbers, CycleCountSupport support)
    {
        _locations = locations;
        _assignees = assignees;
        _legalEntities = legalEntities;
        _numbers = numbers;
        _support = support;
    }

    /// <summary>The input normalized (trimmed note, de-duplicated lists keep their order) — shape errors are the validator's.</summary>
    public static CycleCountForm Normalize(CycleCountFormInput input) => new(input.WarehouseId,
        Enum.Parse<CycleCountType>(input.CountType, ignoreCase: false), input.Blind, input.ScopeLocationIds.Distinct().ToList(),
        input.ScopeSkuIds.Distinct().ToList(), input.PlannedDate, input.AssignedTo, CycleCountSupport.Trimmed(input.Note));

    /// <summary>Pack §12: the warehouse (LOCATION Warehouse, Active, this company), the scope nodes, the planned date, the assignee.</summary>
    public async Task<Response<T>?> FormAsync<T>(CycleCountForm form, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(_support.Now().UtcDateTime);
        if (DateOnly.ParseExact(form.PlannedDate, "yyyy-MM-dd", CultureInfo.InvariantCulture) < today)
            return CycleCountSupport.Invalid<T>("plannedDate must be today (UTC) or later.");

        var warehouse = await _locations.FindAsync(form.WarehouseId, ct);
        if (warehouse is null || warehouse.IsDeleted || warehouse.Type != LocationType.Warehouse || warehouse.Status != LocationStatus.Active)
        {
            return CycleCountSupport.Fail<T>(InventoryErrorCodes.UnknownLocation, "The warehouse does not exist as an active warehouse of the selected company.", 422,
                new Dictionary<string, object?> { ["locationId"] = form.WarehouseId.ToString("D") });
        }

        if (form.ScopeLocationIds.Count > 0)
        {
            var nodes = (await _locations.FindManyAsync(form.ScopeLocationIds.ToList(), ct)).ToDictionary(n => n.Id);
            foreach (var id in form.ScopeLocationIds)
            {
                if (!nodes.TryGetValue(id, out var node) || node.IsDeleted || node.Status != LocationStatus.Active
                    || !CycleCountRules.ScopeNodeTypes.Contains(node.Type) || !node.AncestorIds.Contains(form.WarehouseId))
                {
                    return CycleCountSupport.Fail<T>(CycleCountErrorCodes.ScopeNodeInvalid,
                        "Each scope node must be an active storage location, zone or bin under the selected warehouse.", 422,
                        new Dictionary<string, object?> { ["locationId"] = id.ToString("D") });
                }
            }
        }

        return await AssigneeAsync<T>(form.AssignedTo, ct);
    }

    /// <summary>Pack §12 <c>AssignedTo</c>: an active user of this tenant (DL-1 form; DL-3 recount request). null = none or valid.</summary>
    public async Task<Response<T>?> AssigneeAsync<T>(Guid? assignedTo, CancellationToken ct)
    {
        if (assignedTo is not { } assignee) return null;
        return await _assignees.CheckAsync(assignee, ct) switch
        {
            CycleCountAssigneeCheck.Valid => null,
            CycleCountAssigneeCheck.Invalid => CycleCountSupport.Fail<T>(CycleCountErrorCodes.AssigneeInvalid, "The assignee is not an active user of this tenant.", 422,
                new Dictionary<string, object?> { ["assignedTo"] = assignee.ToString("D") }),
            _ => CycleCountSupport.Fail<T>(CycleCountErrorCodes.AssigneeDirectoryUnavailable, "The assignee could not be checked; nothing was written.", 503)
        };
    }

    /// <summary>The company prefix (MDM legal-entity <c>Code</c>, <c>^[A-Z]{2,5}$</c>) or the refusal.</summary>
    public async Task<(string? Code, Response<T>? Refusal)> CompanyCodeAsync<T>(CancellationToken ct)
    {
        LegalEntityName? company;
        try
        {
            company = await _legalEntities.GetLegalEntityNameAsync(_support.LegalEntityId, ct);
        }
        catch (LegalEntityMasterUnavailableException)
        {
            company = null;
        }

        if (company is null)
        {
            return (null, CycleCountSupport.Fail<T>(InventoryErrorCodes.LegalEntityMasterUnavailable,
                "The company's code could not be read from MDM; no count was opened.", 503));
        }

        return WarehouseRecordCodeRules.IsValidCompanyCode(company.Code)
            ? (company.Code, null)
            : (null, CycleCountSupport.Fail<T>(CycleCountErrorCodes.CompanyCodeInvalid, "Correct the company code (2-5 capital letters A-Z) before opening a count.", 422,
                new Dictionary<string, object?> { ["companyCode"] = company.Code }));
    }

    /// <summary>
    /// SB3/F-1: the number is taken AFTER the replay lookup and every business rule and BEFORE the write transaction; the counter does not
    /// join the transaction, so a number whose write fails is skipped, never reissued. A refused type (CNT missing from the counter's list —
    /// AC-12 sabotage) is the same 503 as an unreachable counter: no count is opened.
    /// </summary>
    public async Task<(string? Number, Response<T>? Refusal)> NumberAsync<T>(string companyCode, CancellationToken ct)
    {
        try
        {
            return (await _numbers.NextAsync(companyCode, CycleCountContract.RecordType, ct), null);
        }
        catch (Exception ex) when (ex is WarehouseRecordCodeUnavailableException or ArgumentException)
        {
            return (null, CycleCountSupport.Fail<T>(CycleCountErrorCodes.RecordCodeUnavailable, "The count number could not be issued; no count was opened.", 503));
        }
    }
}
