using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Common.Upstream;
using Diten.InventoryService.Application.Features.WavePlanning.Ports;
using Diten.InventoryService.Application.Features.Warehouse.Ports;
using Diten.InventoryService.Application.Features.Waves;
using Diten.InventoryService.Domain.Warehouse.Rules;
using Diten.InventoryService.Domain.WavePlanning.Enums;
using Diten.InventoryService.Domain.WavePlanning.Repositories;
using Diten.InventoryService.Domain.WavePlanning.Rules;

namespace Diten.InventoryService.Application.Features.WavePlanning.Services;

/// <summary>The Draft form after normalization (pack §12): the parsed type, the trimmed texts.</summary>
public sealed record WaveForm(Guid WarehouseId, WaveType WaveType, string CutoffLocal, string? ShipToFilter, int? MaxOrders, int? MaxLines,
    Guid? ResponsibleUserId, string? Note)
{
    /// <summary>The form as fingerprint fields (operation and target are added by the caller).</summary>
    public (string Name, string? Value)[] Fields() =>
    [
        ("warehouseId", WarehouseId.ToString("D")), ("waveType", WaveType.ToString()), ("cutoffLocal", CutoffLocal), ("shipToFilter", ShipToFilter),
        ("maxOrders", WaveSupport.Int(MaxOrders)), ("maxLines", WaveSupport.Int(MaxLines)), ("responsibleUserId", ResponsibleUserId?.ToString("D")),
        ("note", Note)
    ];
}

/// <summary>
/// The business checks of a Draft form and the record number (pack §4.1, §8.2 rule 1, §8.4, §8.14, §12): the warehouse on LOCATION
/// (company-scoped), the warehouse's wave setting, the cutoff in the warehouse's time zone, the responsible user in the directory, the company
/// prefix from MDM and the number from MOD-0178's temporary counter with type <c>WAV</c> (Q-4 = A; 0180 writes no counter of its own).
/// </summary>
public sealed class WaveChecks
{
    private readonly IWaveInventoryReader _inventory;
    private readonly IWaveRepository _waves;
    private readonly IWaveResponsibleDirectory _directory;
    private readonly ILegalEntityMasterClient _legalEntities;
    private readonly IWarehouseRecordCodeAllocator _numbers;
    private readonly WaveSupport _support;

    public WaveChecks(IWaveInventoryReader inventory, IWaveRepository waves, IWaveResponsibleDirectory directory, ILegalEntityMasterClient legalEntities,
        IWarehouseRecordCodeAllocator numbers, WaveSupport support)
    {
        _inventory = inventory;
        _waves = waves;
        _directory = directory;
        _legalEntities = legalEntities;
        _numbers = numbers;
        _support = support;
    }

    /// <summary>The input normalized (trimmed texts; an empty text is absent) — shape errors are the validator's.</summary>
    public static WaveForm Normalize(WaveFormInput input) => new(input.WarehouseId, Enum.Parse<WaveType>(input.WaveType, ignoreCase: false), input.CutoffLocal,
        WaveSupport.Trimmed(input.ShipToFilter), input.MaxOrders, input.MaxLines, input.ResponsibleUserId, WaveSupport.Trimmed(input.Note));

    /// <summary>The warehouse as an Active LOCATION <c>Warehouse</c> of the company, or the refusal (<paramref name="unknownStatus"/> / 503).</summary>
    public async Task<(WaveWarehouse? Warehouse, Response<T>? Refusal)> WarehouseAsync<T>(Guid warehouseId, int unknownStatus, CancellationToken ct)
    {
        try
        {
            var warehouse = await _inventory.WarehouseAsync(warehouseId, ct);
            return warehouse is null ? (null, WaveSupport.UnknownWarehouse<T>(warehouseId, unknownStatus)) : (warehouse, null);
        }
        catch (WaveInventoryUnavailableException)
        {
            return (null, WaveSupport.InventoryUnavailable<T>());
        }
    }

    /// <summary>
    /// Pack §8.2 rule 1: the warehouse must be wave managed AND carry a time zone — otherwise 422 <c>WAVE_SETTINGS_REQUIRED</c>. Answers the
    /// warehouse's time zone.
    /// </summary>
    public async Task<(string? TimeZoneId, Response<T>? Refusal)> ManagedTimeZoneAsync<T>(Guid warehouseId, CancellationToken ct)
    {
        var settings = await _waves.FindSettingsAsync(warehouseId, ct);
        return settings is { WaveManaged: true, TimeZoneId: { Length: > 0 } zone } ? (zone, null) : (null, WaveSupport.SettingsRequired<T>());
    }

    /// <summary>
    /// Pack §8.4: the local cutoff in <paramref name="timeZoneId"/> — never the server's zone. A local time in the daylight-saving gap, or a
    /// cutoff more than one hour in the past, is 400 <c>WAVE_CUTOFF_INVALID</c>.
    /// </summary>
    public (WaveCutoff? Cutoff, Response<T>? Refusal) Cutoff<T>(string cutoffLocal, string timeZoneId)
    {
        var cutoff = WaveCutoffRules.ToUtc(cutoffLocal, timeZoneId);
        switch (cutoff.Outcome)
        {
            case WaveCutoffOutcome.Converted when !WaveCutoffRules.IsTooOld(cutoff.AtUtc, _support.Now()):
                return (cutoff, null);
            case WaveCutoffOutcome.Converted:
                return (null, WaveSupport.Fail<T>(WaveErrorCodes.CutoffInvalid, "The cutoff may be at most one hour in the past.", 400));
            case WaveCutoffOutcome.InGap:
                return (null, WaveSupport.Fail<T>(WaveErrorCodes.CutoffInvalid,
                    "This local time does not exist in the warehouse's time zone (daylight-saving change); choose another cutoff.", 400,
                    new Dictionary<string, object?> { ["timeZoneId"] = timeZoneId }));
            case WaveCutoffOutcome.UnknownTimeZone:
                return (null, WaveSupport.SettingsRequired<T>());
            default:
                return (null, WaveSupport.Invalid<T>("cutoffLocal must be a local date-time (yyyy-MM-ddTHH:mm)."));
        }
    }

    /// <summary>Pack §12: the responsible user is an active user of the tenant (422) — the directory must answer (503).</summary>
    public async Task<Response<T>?> ResponsibleAsync<T>(Guid? responsibleUserId, CancellationToken ct)
    {
        if (responsibleUserId is not { } user) return null;
        return await _directory.CheckAsync(user, ct) switch
        {
            WaveResponsibleCheck.Valid => null,
            WaveResponsibleCheck.Invalid => WaveSupport.Fail<T>(WaveErrorCodes.ResponsibleInvalid, "The responsible user is not an active user of this tenant.", 422,
                new Dictionary<string, object?> { ["responsibleUserId"] = user.ToString("D") }),
            _ => WaveSupport.Fail<T>(WaveErrorCodes.ResponsibleDirectoryUnavailable, "The responsible user could not be checked; nothing was written.", 503)
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
            return (null, WaveSupport.Fail<T>(InventoryErrorCodes.LegalEntityMasterUnavailable,
                "The company's code could not be read from MDM; no wave was created.", 503));
        }

        return WarehouseRecordCodeRules.IsValidCompanyCode(company.Code)
            ? (company.Code, null)
            : (null, WaveSupport.Fail<T>(WaveErrorCodes.CompanyCodeInvalid, "Correct the company code (2-5 capital letters A-Z) before creating a wave.", 422,
                new Dictionary<string, object?> { ["companyCode"] = company.Code }));
    }

    /// <summary>
    /// The number is taken AFTER the replay lookup and every business rule and BEFORE the write transaction; the counter does not join the
    /// transaction, so a number whose write fails is skipped, never reissued. A refused type (WAV missing from the counter's list — AC-10
    /// sabotage) is the same 503 as an unreachable counter: no wave is created.
    /// </summary>
    public async Task<(string? Number, Response<T>? Refusal)> NumberAsync<T>(string companyCode, CancellationToken ct)
    {
        try
        {
            return (await _numbers.NextAsync(companyCode, WaveContract.RecordType, ct), null);
        }
        catch (Exception ex) when (ex is WarehouseRecordCodeUnavailableException or ArgumentException)
        {
            return (null, WaveSupport.Fail<T>(WaveErrorCodes.RecordCodeUnavailable, "The wave number could not be issued; no wave was created.", 503));
        }
    }
}
