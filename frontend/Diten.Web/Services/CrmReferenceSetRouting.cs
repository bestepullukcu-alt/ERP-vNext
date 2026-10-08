using System.Collections.Concurrent;

namespace Diten.Web.Services;

/// <summary>
/// WP-BRD-TENANT-CRM-SETS (step 2) — process-wide memory of the set codes the Platform said are NOT on its consumable-sets
/// list (404 <c>reference_set_not_tenant_accessible</c>). Such a set is read on the old consumer path directly from then on,
/// so a set outside the list costs one extra request per process, not one per dropdown.
///
/// <para>Deliberately NOT a copy of the Platform list: the Web learns only what the Platform answered. The list lives in
/// Platform configuration (<c>BusinessReferenceData:ConsumableSets</c>); the drift guard
/// (<c>CrmReferenceSetDriftGuardTests</c>) keeps the CRM Web set codes inside it. Process lifetime, like CRM's
/// <c>ConsumableReferenceSetRouting</c>: when the Platform list grows, the Web is restarted.</para>
/// </summary>
public sealed class CrmReferenceSetRouting
{
    /// <summary>The process-wide instance every CRM controller's reader uses.</summary>
    public static CrmReferenceSetRouting Shared { get; } = new();

    private readonly ConcurrentDictionary<string, byte> _notConsumable = new(StringComparer.OrdinalIgnoreCase);

    public bool IsKnownNotConsumable(string setCode) => _notConsumable.ContainsKey(Normalize(setCode));

    public void MarkNotConsumable(string setCode) => _notConsumable.TryAdd(Normalize(setCode), 0);

    private static string Normalize(string setCode) => (setCode ?? string.Empty).Trim();
}
