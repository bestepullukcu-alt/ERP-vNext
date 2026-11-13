using System.Globalization;
using Diten.CrmService.Domain.Entities.Commercial;
using Diten.CrmService.Domain.Repositories.Commercial;

namespace Diten.CrmService.Application.Features.Commercial.Pricing;

/// <summary>
/// WP-VW-W6 W6-BE-b — MOD-0156 §4.7 / §12: every write and every transition is appended to the change log (who, when,
/// old → new). Append only; a failed append never undoes the write it describes (it is logged by the caller's pipeline as
/// an exception — the central audit record of the command is the other trail).
/// </summary>
public sealed class PricingChangeLogWriter
{
    private readonly IPricingChangeLogRepository _log;
    private readonly TimeProvider _clock;

    public PricingChangeLogWriter(IPricingChangeLogRepository log, TimeProvider? clock = null)
    {
        _log = log;
        _clock = clock ?? TimeProvider.System;
    }

    public Task WriteAsync(Guid tenantId, string entityType, Guid entityId, int entityVersion, Guid? parentId, string action,
        string? by, IReadOnlyList<PricingFieldChange> changes, CancellationToken ct)
        => _log.AppendAsync(new[] { Entry(tenantId, entityType, entityId, entityVersion, parentId, action, by, changes) }, ct);

    public Task WriteManyAsync(IReadOnlyList<PricingChangeLog> entries, CancellationToken ct)
        => entries.Count == 0 ? Task.CompletedTask : _log.AppendAsync(entries, ct);

    public PricingChangeLog Entry(Guid tenantId, string entityType, Guid entityId, int entityVersion, Guid? parentId, string action,
        string? by, IReadOnlyList<PricingFieldChange> changes)
        => new()
        {
            TenantId = tenantId, EntityType = entityType, EntityId = entityId, EntityVersion = entityVersion, ParentId = parentId,
            Action = action, ByUserId = by, At = _clock.GetUtcNow(), Changes = changes.ToList()
        };

    /// <summary>The fields whose value differs (old → new), in the given order.</summary>
    public static List<PricingFieldChange> Diff(params (string Field, object? Old, object? New)[] fields)
        => fields.Where(f => !Equals(Text(f.Old), Text(f.New)))
            .Select(f => new PricingFieldChange { Field = f.Field, Old = Text(f.Old), New = Text(f.New) })
            .ToList();

    /// <summary>The values of a new record (old empty).</summary>
    public static List<PricingFieldChange> Created(params (string Field, object? New)[] fields)
        => fields.Where(f => Text(f.New) is not null)
            .Select(f => new PricingFieldChange { Field = f.Field, Old = null, New = Text(f.New) })
            .ToList();

    public static string? Text(object? value) => value switch
    {
        null => null,
        string s => string.IsNullOrEmpty(s) ? null : s,
        decimal d => d.ToString(CultureInfo.InvariantCulture),
        DateOnly day => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTimeOffset at => at.ToString("O", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString()
    };
}

public static class PricingChangeActions
{
    public const string Created = "created";
    public const string Updated = "updated";
    public const string VersionCreated = "version-created";
    public const string Discarded = "discarded";
    public const string Submitted = "submitted";
    public const string Published = "published";
    public const string Returned = "returned";
    public const string Refused = "refused";
    public const string Withdrawn = "withdrawn";
    public const string Superseded = "superseded";
    public const string Retired = "retired";
    public const string RetirementRequested = "retirement-requested";
    public const string ItemAdded = "item-added";
    public const string ItemChanged = "item-changed";
    public const string ItemRemoved = "item-removed";
    public const string Imported = "imported";
}
