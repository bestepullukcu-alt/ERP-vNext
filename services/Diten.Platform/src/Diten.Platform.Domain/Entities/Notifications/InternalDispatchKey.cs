using Diten.Platform.Common.Persistence;

namespace Diten.Platform.Domain.Entities.Notifications;

/// <summary>
/// WP-VW-W5 W5-FIX2 (F-1) — "this internal event dispatch was already asked for". A service that retries its call (CRM
/// after a time-out) sends the same <see cref="Key"/>; unique per (tenant, <see cref="SourceModule"/>, key), so a second
/// call opens no second dispatch and gets the first call's answer (<see cref="ResultJson"/>) back.
///
/// <para>The row is CLAIMED before any dispatch is written (<see cref="InternalDispatchKeyStates.Pending"/>), then either
/// completed with the answer or released when nothing was accepted (a refused call may be tried again). A claim left
/// pending by a process that died is taken over after a while, under a new <see cref="ClaimToken"/>. Never deleted.</para>
/// </summary>
public sealed class InternalDispatchKey : TenantScopedEntity
{
    /// <summary>The asking module, upper case (keys are scoped by it: two modules never collide).</summary>
    public required string SourceModule { get; set; }

    public required string Key { get; set; }

    public string State { get; set; } = InternalDispatchKeyStates.Pending;

    /// <summary>Which claim holds the row: a completion or a release only lands for the token that claimed it.</summary>
    public Guid ClaimToken { get; set; }

    /// <summary>When the claim was taken, as UTC ticks: a plain Int64 (the AuditOutboxTemporalMigrationState precedent), so
    /// no DateTimeOffset array enters the persisted inventory and the stale-claim comparison is a number.</summary>
    public long ClaimedAtUtcTicks { get; set; }

    /// <summary>The first call's answer, as JSON; only when <see cref="State"/> is done.</summary>
    public string? ResultJson { get; set; }

    public long? CompletedAtUtcTicks { get; set; }
}

public static class InternalDispatchKeyStates
{
    public const string Pending = "pending";
    public const string Done = "done";
    public const string Released = "released";
}
