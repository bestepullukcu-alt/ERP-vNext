using Diten.CrmService.Application.Common;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;

namespace Diten.CrmService.Application.Features.VisitPlanning;

/// <summary>
/// MOD-0155 FU05 — "pharmacies of a clinic" (§4.1 ②). Reads MOD-0149 <see cref="IAccountRelationship"/> (bidirectional)
/// and OFFERS the pharmacies related to a chosen clinic/hospital. It is READ-only and the relationship <b>OFFERS, it
/// does not auto-add</b> — the selection stays MANUAL, and a pharmacy with no relationship is still directly selectable
/// (the link is context, never a precondition — FU01 D9 / AC-SELECT-3). Nothing is mutated.
/// </summary>
public sealed class PharmacyExpander
{
    private readonly ITenantContext _tenant;
    private readonly IAccountRelationshipRepository _relationships;
    private readonly IAccountRepository _accounts;

    public PharmacyExpander(
        ITenantContext tenant, IAccountRelationshipRepository relationships, IAccountRepository accounts)
    {
        _tenant = tenant;
        _relationships = relationships;
        _accounts = accounts;
    }

    /// <summary>The pharmacy accounts related to <paramref name="clinicAccountId"/>, in either direction. An account is
    /// treated as a pharmacy when its account-type carries "pharmac" (the published Hospital↔Pharmacy link's target).
    /// Empty when the clinic has no pharmacy relationship — that is a valid answer, never an error.</summary>
    public async Task<IReadOnlyList<PharmacyOffer>> OfferForClinicAsync(
        Guid clinicAccountId, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId || clinicAccountId == Guid.Empty)
        {
            return Array.Empty<PharmacyOffer>();
        }

        var links = await _relationships.ListByAccountAsync(tenantId, clinicAccountId, cancellationToken);
        var relatedIds = links
            .Select(l => l.SourceAccountId == clinicAccountId ? l.TargetAccountId : l.SourceAccountId)
            .Where(id => id != Guid.Empty && id != clinicAccountId)
            .Distinct()
            .ToList();

        var offers = new List<PharmacyOffer>();
        foreach (var id in relatedIds)
        {
            var account = await _accounts.GetByIdAsync(tenantId, id, cancellationToken);
            if (account is null || account.IsDeleted)
            {
                continue;
            }

            if (IsPharmacy(account.AccountType))
            {
                offers.Add(new PharmacyOffer(account.Id, account.AccountName, account.AccountType));
            }
        }

        return offers;
    }

    private static bool IsPharmacy(string? accountType)
        => !string.IsNullOrWhiteSpace(accountType)
           && accountType.Contains("pharmac", StringComparison.OrdinalIgnoreCase);
}

/// <summary>One offered pharmacy (context only). Selecting it stays a manual act.</summary>
public sealed record PharmacyOffer(Guid AccountId, string AccountName, string AccountType);
