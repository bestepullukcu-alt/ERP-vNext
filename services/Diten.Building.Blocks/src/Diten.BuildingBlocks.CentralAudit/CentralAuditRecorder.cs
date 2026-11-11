using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Diten.BuildingBlocks.CentralAudit;

/// <summary>
/// For a repository that owns a Mongo transaction: write the CURRENT marked command's central-audit event into that
/// transaction, so it commits with the business data or not at all (AUD-001 K2: record and data together). One line
/// inside the transaction callback:
/// <code>await _centralAudit.EnlistAsync(session, entityId, ct);</code>
/// No marked command in flight (a background job, an unmarked command) → no-op. Called again in a later transaction of
/// the same command → no-op (the row exists once).
/// </summary>
public interface ICentralAuditTransaction
{
    Task EnlistAsync(IClientSessionHandle session, Guid? entityId = null, CancellationToken ct = default);
}

/// <summary>
/// The same enlistment without a constructor dependency — one added line in a repository's transaction callback:
/// <code>await CentralAuditTransaction.EnlistAsync(s, ct: ct);</code>
/// It reaches the recorder of the marked command that is running on this async flow (set by
/// <see cref="CentralAuditBehavior{TRequest,TResponse}"/>); with none — a sweeper, an unmarked command, a host without
/// <c>AddCentralAudit</c>, a unit test that builds the repository by hand — it does nothing.
/// </summary>
public static class CentralAuditTransaction
{
    public static Task EnlistAsync(IClientSessionHandle session, Guid? entityId = null, CancellationToken ct = default) =>
        CentralAuditRecorder.Ambient.Value is { } recorder
            ? recorder.EnlistAsync(session, entityId, ct)
            : Task.CompletedTask;
}

/// <summary>
/// Request-scoped: the events of the marked commands in flight (a stack — a handler may send another marked command).
/// <see cref="CentralAuditBehavior{TRequest,TResponse}"/> opens and closes them; a repository enlists the innermost one
/// in its transaction.
///
/// <para>THE THREE ENDINGS.</para>
/// <list type="bullet">
/// <item>Enlisted, and the command succeeded → the row committed with the first business transaction as
/// <see cref="CentralAuditOutboxStatus.AwaitingCommand"/>; it is confirmed deliverable now.</item>
/// <item>Enlisted, and the command failed → if its transaction aborted the row is gone with it. If a transaction of the
/// command had committed (it can commit without business data — "not found" answered inside it — or before a later step
/// of a multi-transaction command fails), the row is kept as <see cref="CentralAuditOutboxStatus.Abandoned"/> and never
/// delivered: a failed command has no central record. Its partial writes stay visible in the module's own history.</item>
/// <item>Not enlisted (the command owns no transaction) → succeeded: the row is written right after the command
/// (⚠ the gap: a crash between the command and this write loses the record — logged, counted in the evidence);
/// failed: nothing.</item>
/// </list>
/// </summary>
public sealed class CentralAuditRecorder : ICentralAuditTransaction
{
    /// <summary>The recorder of the marked command running on this async flow (see <see cref="CentralAuditTransaction"/>).</summary>
    internal static readonly AsyncLocal<CentralAuditRecorder?> Ambient = new();

    private readonly Stack<PendingCentralAuditEvent> _inFlight = new();
    private readonly ICentralAuditOutboxStore _store;
    private readonly ICentralAuditScope _scope;
    private readonly CentralAuditSource _source;
    private readonly TimeProvider _clock;
    private readonly bool _requireEnlistment;
    private readonly ILogger<CentralAuditRecorder> _logger;

    public CentralAuditRecorder(
        ICentralAuditOutboxStore store,
        ICentralAuditScope scope,
        CentralAuditSource source,
        TimeProvider clock,
        Microsoft.Extensions.Options.IOptions<CentralAuditOptions> options,
        ILogger<CentralAuditRecorder> logger)
    {
        _store = store;
        _scope = scope;
        _source = source;
        _clock = clock;
        _requireEnlistment = options.Value.RequireEnlistment;
        _logger = logger;
    }

    /// <summary>Opens the event when the marked command starts — the scope is read NOW, from the caller's resolved context.</summary>
    internal PendingCentralAuditEvent? Begin(ICentralAuditDeclaration declaration, string requestType)
    {
        if (!_scope.IsResolved)
        {
            // No tenant, no record: Platform files every event under a tenant. Loud, never silent (AUD-001 §4.3).
            _logger.LogError(
                "Central audit: {RequestType} ran without a resolved caller scope; no central record can be filed for it.",
                requestType);
            return null;
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        var eventId = Guid.NewGuid();
        var metadata = declaration.CentralAudit;
        var record = new CentralAuditOutboxRecord
        {
            Id = eventId,
            TenantId = _scope.TenantId,
            CompanyId = _scope.CompanyId,
            ActorId = _scope.ActorId,
            ActorDisplayName = _scope.ActorDisplayName,
            ActorType = _scope.ActorType,
            SourceService = _source.Service,
            SourceModule = metadata.SourceModule,
            Category = metadata.Category,
            Operation = metadata.Operation,
            EntityType = metadata.EntityType,
            RequestType = requestType,
            CorrelationId = _scope.CorrelationId == Guid.Empty ? eventId : _scope.CorrelationId,
            OccurredAtUtc = now,

            Status = CentralAuditOutboxStatus.Pending,
            NextAttemptAtUtc = now,
            CreatedAtUtc = now
        };

        var pending = new PendingCentralAuditEvent(record, declaration is ICentralAuditOutsideTransaction);
        _inFlight.Push(pending);
        return pending;
    }

    public async Task EnlistAsync(IClientSessionHandle session, Guid? entityId = null, CancellationToken ct = default)
    {
        if (_inFlight.Count == 0)
        {
            return;
        }

        var pending = _inFlight.Peek();
        var row = pending.Snapshot(entityId);
        row.WrittenInCommandTransaction = true;
        row.Status = CentralAuditOutboxStatus.AwaitingCommand;
        await _store.InsertInTransactionAsync(session, row, ct);

        // Set on every attempt: WithTransactionAsync re-runs the callback after a transient conflict, and an attempt that
        // aborted took its row with it — only a committed attempt's row exists.
        pending.Enlisted = true;
        pending.Record.EntityId ??= entityId;
    }

    /// <summary>The command succeeded.</summary>
    internal async Task CompleteAsync(PendingCentralAuditEvent pending, Guid? entityId, CancellationToken ct)
    {
        Pop(pending);
        var record = pending.Record;

        if (pending.Enlisted)
        {
            // Entity id first: the row is not deliverable until it is confirmed, so the dispatcher never sends it without one.
            if (entityId is { } late)
            {
                await TryAsync(() => _store.SetEntityIdIfMissingAsync(record.Id, late, CancellationToken.None), record, "entity id");
            }

            await TryAsync(() => _store.ConfirmAsync(record.Id, CancellationToken.None), record, "confirmation");
            return;
        }

        if (_requireEnlistment && !pending.OutsideTransaction)
        {
            // Every write of this service enlists; a success that enlisted nothing wrote nothing (idempotent replay, no-op).
            _logger.LogInformation(
                "Central audit: {RequestType} succeeded without writing (no transaction enlisted the event); no central record. TenantId={TenantId} EventId={EventId}",
                record.RequestType,
                record.TenantId,
                record.Id);
            return;
        }

        record.EntityId = entityId ?? record.EntityId;

        // ⚠ THE GAP, BY NAME: the command's data is already written. If this insert fails (or the process dies between the
        // command and this line) the event is lost and only this error line says so. A command that owns a Mongo
        // transaction closes the gap with EnlistAsync. CancellationToken.None: a client that hung up does not cancel the record.
        await TryAsync(() => _store.InsertAsync(record, CancellationToken.None), record, "outbox row");
    }

    /// <summary>The command failed (threw, or answered a failed envelope).</summary>
    internal async Task AbandonAsync(PendingCentralAuditEvent pending)
    {
        Pop(pending);
        if (pending.Enlisted)
        {
            // Matches nothing when the enlisting transaction aborted; otherwise the committed row is kept, never delivered.
            await TryAsync(() => _store.AbandonAsync(pending.Record.Id, CancellationToken.None), pending.Record, "abandonment");
        }
    }

    private void Pop(PendingCentralAuditEvent pending)
    {
        if (_inFlight.Count > 0 && ReferenceEquals(_inFlight.Peek(), pending))
        {
            _inFlight.Pop();
        }
    }

    private async Task TryAsync(Func<Task> write, CentralAuditOutboxRecord record, string what)
    {
        try
        {
            await write();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Central audit: the {What} could not be written after the command ended. TenantId={TenantId} EventId={EventId} RequestType={RequestType} EntityType={EntityType}",
                what,
                record.TenantId,
                record.Id,
                record.RequestType,
                record.EntityType);
        }
    }
}

internal sealed class PendingCentralAuditEvent(CentralAuditOutboxRecord record, bool outsideTransaction)
{
    public CentralAuditOutboxRecord Record { get; } = record;

    public bool OutsideTransaction { get; } = outsideTransaction;

    public bool Enlisted { get; set; }

    /// <summary>A copy for one transaction attempt — the in-memory record is never the object a driver serialized and tracked.</summary>
    public CentralAuditOutboxRecord Snapshot(Guid? entityId)
    {
        var copy = Record.Copy();
        copy.EntityId ??= entityId;
        return copy;
    }
}

/// <summary>The service this package files events for (Platform <c>SourceService</c>).</summary>
public sealed record CentralAuditSource(string Service);
