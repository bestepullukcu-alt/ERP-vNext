using System.Collections.Concurrent;

namespace Diten.CrmService.Infrastructure.ReferenceValidation;

/// <summary>
/// WP-BRD-TENANT-CRM-SETS — process-wide memory of the set codes the Platform said are NOT on its consumable-sets list
/// (404 <c>reference_set_not_tenant_accessible</c>). Such a set is read on the Platform consumer path directly from then
/// on, so a set outside the list costs one extra request per process, not one per read.
///
/// <para>Deliberately NOT a copy of the Platform list: CRM learns only what the Platform answered. The list itself lives in
/// Platform configuration (<c>BusinessReferenceData:ConsumableSets</c>) and the drift guard keeps CRM's set codes inside
/// it. Registered as a singleton because the validator is a typed HttpClient (transient).</para>
/// </summary>
public sealed class ConsumableReferenceSetRouting
{
    private readonly ConcurrentDictionary<string, byte> _notConsumable = new(StringComparer.OrdinalIgnoreCase);

    public bool IsKnownNotConsumable(string setCode) => _notConsumable.ContainsKey(Normalize(setCode));

    public void MarkNotConsumable(string setCode) => _notConsumable.TryAdd(Normalize(setCode), 0);

    private static string Normalize(string setCode) => (setCode ?? string.Empty).Trim();
}
