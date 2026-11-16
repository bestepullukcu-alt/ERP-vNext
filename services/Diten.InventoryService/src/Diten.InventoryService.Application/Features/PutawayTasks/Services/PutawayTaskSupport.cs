using System.Globalization;
using System.Text.Json;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Warehouse;
using Diten.InventoryService.Application.Features.Warehouse.Services;
using Diten.InventoryService.Domain.Rules;
using Diten.InventoryService.Domain.Warehouse.Entities;
using Diten.InventoryService.Domain.Warehouse.Enums;
using Microsoft.Extensions.Options;

namespace Diten.InventoryService.Application.Features.PutawayTasks.Services;

/// <summary>
/// What every MOD-0178 task command shares on top of <see cref="WarehouseSupport"/>: the clock and the putaway settings, the timeline row
/// factory (a write's rows take the task's next <c>Seq</c> values in the same compare-and-set write), the wire formats and the WMS refusals.
/// </summary>
public sealed class PutawayTaskSupport
{
    public const string DateTimeFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    private readonly WarehouseSupport _support;

    public PutawayTaskSupport(WarehouseSupport support, IOptions<WarehousePutawayOptions> options)
    {
        _support = support;
        Options = options.Value;
    }

    public WarehousePutawayOptions Options { get; }

    public Guid TenantId => _support.TenantId;

    public Guid LegalEntityId => _support.LegalEntityId;

    public Guid UserId => _support.UserId;

    public string? ActorName => _support.ActorName;

    public Guid CorrelationId => _support.CorrelationId;

    public DateTimeOffset Now() => _support.Now();

    public TimeSpan Lease => TimeSpan.FromSeconds(Options.InFlightLeaseSeconds);

    public TimeSpan LongPending => TimeSpan.FromMinutes(Options.LongPendingMinutes);

    // ── timeline rows ─────────────────────────────────────────────────────────────────────────────────────

    /// <summary>A timeline row of <paramref name="task"/> by the caller (its <c>Seq</c> = the task's next event seq).</summary>
    public WarehouseTaskEvent Event(WarehouseTask task, WarehouseTaskEventKind kind, DateTimeOffset at, IReadOnlyDictionary<string, string?>? data = null,
        string? key = null, string? op = null, string? fingerprint = null)
        => NewEvent(task, kind, at, data, UserId, ActorName, system: false, key, op, fingerprint);

    /// <summary>A system row (the reconciler's defensive branch): no actor.</summary>
    public WarehouseTaskEvent SystemEvent(WarehouseTask task, WarehouseTaskEventKind kind, DateTimeOffset at, IReadOnlyDictionary<string, string?>? data = null)
        => NewEvent(task, kind, at, data, null, null, system: true, null, null, null);

    private WarehouseTaskEvent NewEvent(WarehouseTask task, WarehouseTaskEventKind kind, DateTimeOffset at, IReadOnlyDictionary<string, string?>? data,
        Guid? actor, string? actorName, bool system, string? key, string? op, string? fingerprint) => new()
    {
        Id = Guid.NewGuid(),
        CreatedAt = at,
        TaskId = task.Id,
        Seq = ++task.EventSeq,
        Kind = kind,
        ActorUserId = actor,
        ActorName = actorName,
        System = system,
        AtUtc = at,
        Data = data is null ? [] : new Dictionary<string, string?>(data, StringComparer.Ordinal),
        CorrelationId = CorrelationId,
        IdempotencyKey = key,
        RequestOp = op,
        RequestFingerprint = fingerprint
    };

    // ── wire ──────────────────────────────────────────────────────────────────────────────────────────────

    public static string Wire(DateTimeOffset value) => value.UtcDateTime.ToString(DateTimeFormat, CultureInfo.InvariantCulture);

    public static string? Wire(DateTimeOffset? value) => value is null ? null : Wire(value.Value);

    public static string Q(decimal value) => InventoryQuantity.ToWire(value);

    public static string Seqs(IEnumerable<int> seqs) => string.Join(",", seqs.Select(s => s.ToString(CultureInfo.InvariantCulture)));

    public static string Int(int value) => value.ToString(CultureInfo.InvariantCulture);

    public static string? Json(object? value) => value is null ? null : JsonSerializer.Serialize(value);

    // ── refusals ──────────────────────────────────────────────────────────────────────────────────────────

    public static Response<T> TaskNotFound<T>() => Response<T>.Fail(WarehouseErrorCodes.TaskNotFound, "Putaway task not found.", 404);

    public static Response<T> Fail<T>(string code, string message, int status, IReadOnlyDictionary<string, object?>? details = null)
        => Response<T>.Fail(code, message, status, details);

    public static Response<T> ReasonRequired<T>(string member)
        => Response<T>.Fail(Traceability.TraceErrorCodes.ReasonRequired, $"{member} is required (10-1000 characters).", 400);

    public static Response<T> Pending<T>()
        => Response<T>.Fail(WarehouseErrorCodes.ConfirmationPending, "A confirmation of this task is waiting for the stock answer; only \"Retry\" is open.", 409);

    public static Response<T> IntegrityLocked<T>(IReadOnlyDictionary<string, object?>? details = null)
        => Response<T>.Fail(WarehouseErrorCodes.PostingIntegrityError,
            "A confirmation of this task holds an integrity error; only the supervisor's \"Resolve\" is open.", 409, details);

    public static Response<T> InvalidTransition<T>(string message) => Response<T>.Fail(WarehouseErrorCodes.InvalidTransition, message, 409);

    public static Response<T> NotAssignee<T>()
        => Response<T>.Fail(WarehouseErrorCodes.NotAssignee, "Only the assigned operator or a supervisor may do this.", 403);
}
