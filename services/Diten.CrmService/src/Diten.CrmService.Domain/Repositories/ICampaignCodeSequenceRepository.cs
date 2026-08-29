namespace Diten.CrmService.Domain.Repositories;

/// <summary>MOD-0165 FU10 — the CampaignCode sequence. One method, atomic, no read-then-write race.</summary>
public interface ICampaignCodeSequenceRepository
{
    /// <summary>Atomically increments and returns the next sequence value for (TenantId, Year).</summary>
    Task<long> NextAsync(Guid tenantId, int year, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the value <see cref="NextAsync"/> WOULD return, without consuming it. Pure read: it neither increments
    /// the counter nor creates the sequence document, so calling it a thousand times leaves no trace.
    /// <para>The answer is therefore indicative, not reserved — a concurrent create can take the number between this
    /// call and a later save. Never use it to assign a code; only to show one.</para>
    /// </summary>
    Task<long> PeekNextAsync(Guid tenantId, int year, CancellationToken cancellationToken);
}
