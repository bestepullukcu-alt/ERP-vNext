using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Common.Upstream;
using Diten.InventoryService.Application.Features.Inventory.Services;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Repositories;
using Diten.InventoryService.Domain.Rules;

namespace Diten.InventoryService.Application.Features.Locations.Services;

/// <summary>
/// What every location write shares (MOD-0173-FU02 §8 / §12): the idempotency-key and <c>If-Match</c> rules, field
/// shapes, the org-unit check (once, on write, BEFORE the transaction — an HTTP call never runs inside one), the
/// 403-vs-404 answer for a parent that is not in the caller's company, and the K-11 history row factory.
/// </summary>
public sealed partial class LocationWriteSupport
{
    private readonly ITenantContext _tenant;
    private readonly ICorrelationContext _correlation;
    private readonly IOrgUnitDirectoryClient _orgUnits;
    private readonly ILocationRepository _locations;

    public LocationWriteSupport(
        ITenantContext tenant,
        ICorrelationContext correlation,
        IOrgUnitDirectoryClient orgUnits,
        ILocationRepository locations)
    {
        _tenant = tenant;
        _correlation = correlation;
        _orgUnits = orgUnits;
        _locations = locations;
    }

    public Guid UserId => _tenant.UserId;

    public static Response<T> Fail<T>(string code, string message, int status) => Response<T>.Fail(code, message, status);

    public static Response<T> NotFound<T>()
        => Response<T>.Fail(LocationErrorCodes.UnknownLocation, "Location not found.", 404);

    public static Response<T> Invalid<T>(string message)
        => Response<T>.Fail(LocationErrorCodes.InvalidRequest, message, 400);

    /// <summary>Header and body key: both present, equal after trim, 1–128 characters (same rule as postMovement).</summary>
    public static string? KeyError(string? header, string? body, out string key)
    {
        key = body?.Trim() ?? string.Empty;
        var headerKey = header?.Trim();
        if (string.IsNullOrEmpty(headerKey) || key.Length == 0)
            return "Both the Idempotency-Key header and body idempotencyKey are required.";
        if (!string.Equals(headerKey, key, StringComparison.Ordinal))
            return "The Idempotency-Key header must equal body idempotencyKey.";
        if (key.Length > InventoryContract.MaxIdempotencyKeyLength)
            return $"idempotencyKey must be 1-{InventoryContract.MaxIdempotencyKeyLength} characters.";
        return null;
    }

    /// <summary><c>W/"n"</c> → n; anything else → −1 (can never match a stored version → 409 after the 404 check).</summary>
    public static int ExpectedVersion(string ifMatch)
    {
        var match = WeakETag().Match(ifMatch.Trim());
        return match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var v) ? v : -1;
    }

    /// <summary>
    /// A location id is the canonical lower-case UUID string this API returns — the exact string the ledger stores, so
    /// stock presence can match it (ASSUMPTION-LOC1-03). Any other spelling is not a location id.
    /// </summary>
    public static bool TryParseId(string? raw, out Guid id)
        => WireParsing.TryParseUuid(raw, out id) && string.Equals(raw, id.ToString("D"), StringComparison.Ordinal);

    public static string? CodeError(string? code)
        => LocationHierarchyRules.IsValidCode(code)
            ? null
            : $"code is required: 1-{LocationHierarchyRules.MaxCodeLength} characters of A-Z a-z 0-9 . _ / -.";

    public static string? NameError(string? name)
        => LocationHierarchyRules.IsValidName(name) ? null : $"name is required: 1-{LocationHierarchyRules.MaxNameLength} characters.";

    public static string? CostCenterError(string? code)
        => code is null || LocationHierarchyRules.IsValidCostCenter(code)
            ? null
            : $"costCenterCode must be 1-{LocationHierarchyRules.MaxCostCenterLength} characters of A-Z a-z 0-9 . _ -.";

    public static string? OperatingModeError(LocationType type, string? mode)
    {
        if (mode is null) return null;
        if (!LocationHierarchyRules.MayHaveOperatingMode(type))
            return "operatingMode is allowed on a Site or a Warehouse only.";
        return LocationHierarchyRules.IsValidOperatingMode(mode) ? null : "operatingMode must be internal or external-integrated.";
    }

    public static string? StockableError(LocationType type, bool stockable)
        => stockable && !LocationHierarchyRules.MayBeStockable(type)
            ? "Only a Bin or a StorageLocation can be stockable."
            : null;

    public static string? OrgUnitShapeError(string? orgUnitRef)
        => orgUnitRef is null || WireParsing.TryParseUuid(orgUnitRef, out _) ? null : "orgUnitRef must be a UUID.";

    public static bool TryParseType(string? raw, out LocationType type)
        => Enum.TryParse(raw, ignoreCase: false, out type) && Enum.IsDefined(type) && !int.TryParse(raw, out _);

    public static bool TryParseStatus(string? raw, out LocationStatus status)
        => Enum.TryParse(raw, ignoreCase: false, out status) && Enum.IsDefined(status) && !int.TryParse(raw, out _);

    /// <summary>
    /// §7 / LOC-11: an org unit is checked once, on write, with Platform. Null = fine; otherwise the refusal
    /// (422 ORG_UNIT_INVALID, or 503 ORG_UNIT_DIRECTORY_UNAVAILABLE — nothing is written either way).
    /// </summary>
    public async Task<Response<T>?> OrgUnitRefusalAsync<T>(string? orgUnitRef, CancellationToken ct)
    {
        if (orgUnitRef is null)
        {
            return null;
        }

        OrgUnitCheck check;
        try
        {
            check = await _orgUnits.CheckAsync(Guid.Parse(orgUnitRef), _tenant.LegalEntityId, ct);
        }
        catch (OrgUnitDirectoryUnavailableException)
        {
            return Fail<T>(LocationErrorCodes.OrgUnitDirectoryUnavailable,
                "The organization-unit directory could not confirm orgUnitRef; nothing was saved. Retry, or save without it.", 503);
        }

        return check == OrgUnitCheck.Valid
            ? null
            : Fail<T>(LocationErrorCodes.OrgUnitInvalid,
                "orgUnitRef is not an active organization unit of this company.", 422);
    }

    /// <summary>A parent id not found in the caller's company: 403 when it is another company's (§8), else 404.</summary>
    public async Task<Response<T>> MissingParentAsync<T>(Guid parentId, CancellationToken ct)
        => await _locations.ExistsInAnotherCompanyAsync(parentId, ct)
            ? Fail<T>(LocationErrorCodes.CrossLeForbidden, "The parent location belongs to another company.", 403)
            : Fail<T>(LocationErrorCodes.UnknownLocation, "The parent location was not found.", 404);

    public LocationChangeEntry History(Guid locationId, LocationChangeAction action, string? field, string? oldValue, string? newValue, DateTimeOffset at) => new()
    {
        Id = Guid.NewGuid(),
        CreatedAt = at,
        LocationId = locationId,
        Action = action,
        Field = field,
        OldValue = oldValue,
        NewValue = newValue,
        ActorUserId = _tenant.UserId,
        CorrelationId = _correlation.CorrelationId,
        LeSource = _tenant.LegalEntitySource,
        OccurredAt = at
    };

    /// <summary>SHA-256 over the canonical, ordered field list (request fingerprint for replay / key reuse).</summary>
    public static string Fingerprint(params (string Name, string? Value)[] fields)
    {
        var canonical = string.Join("\n", fields.Select(f => $"{f.Name}={(f.Value is null ? "∅" : f.Value)}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    public static string? Wire(bool? value) => value?.ToString(CultureInfo.InvariantCulture).ToLowerInvariant();

    public static string? Wire(int? value) => value?.ToString(CultureInfo.InvariantCulture);

    [GeneratedRegex("^W/\"(\\d{1,9})\"$", RegexOptions.CultureInvariant)]
    private static partial Regex WeakETag();
}
