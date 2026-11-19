using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Recall.Ports;
using Diten.InventoryService.Domain.Recall.Entities;
using Diten.InventoryService.Domain.Recall.Rules;

namespace Diten.InventoryService.Application.Features.Recall.Services;

/// <summary>
/// MOD-0177-FU01 (pack §8.3, §8.4) — the customer a record command names, matched against the case's scope: a "D" GUID that is the key of a
/// CRM item matches it in its canonical form (any case); any other value matches ordinally, as before FU01. The record is written with
/// <see cref="Key"/>.
/// </summary>
public sealed record RecallCustomerMatch(string Key, RecallCustomerDisplay.KeyFacts Facts)
{
    public bool IsCrm => Facts.IsCrm;

    /// <param name="customer">The command's customerRef as sent: the GUID rule trims it itself (§8.3); the ordinal match takes it trimmed.</param>
    public static RecallCustomerMatch? Of(IReadOnlyList<RecallScopeItem> shipments, string customer)
    {
        var facts = RecallCustomerDisplay.Facts(shipments);
        if (RecallCustomerKeyRules.TryCanonical(customer, out var canonical) && facts.TryGetValue(canonical, out var crm) && crm.IsCrm)
        {
            return new RecallCustomerMatch(canonical, crm);
        }

        var trimmed = customer.Trim();
        return facts.TryGetValue(trimmed, out var other) ? new RecallCustomerMatch(trimmed, other) : null;
    }

    /// <summary>
    /// The resolution check's refusal (§8.4): Unresolved → 503 RECALL_CUSTOMER_DIRECTORY_UNAVAILABLE, Forbidden → 403
    /// RECALL_CUSTOMER_DIRECTORY_FORBIDDEN (neither carries the key); Resolved / NotFound (Q-7) / no CRM key → none.
    /// </summary>
    public static Response<T>? Refusal<T>(RecallCustomerName? name) => name?.Outcome switch
    {
        RecallCustomerOutcome.Unresolved => Response<T>.Fail(RecallErrorCodes.CustomerDirectoryUnavailable,
            "The CRM customer cannot be read now; nothing was recorded. Try again later.", 503),
        RecallCustomerOutcome.Forbidden => Response<T>.Fail(RecallErrorCodes.CustomerDirectoryForbidden,
            "You may not read this CRM customer (crm.account.lookup-validation); nothing was recorded.", 403),
        _ => null
    };
}
