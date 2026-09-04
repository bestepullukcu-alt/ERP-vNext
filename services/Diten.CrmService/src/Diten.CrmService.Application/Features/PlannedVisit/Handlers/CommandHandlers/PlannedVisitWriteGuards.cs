using Diten.CrmService.Application.Features.PlannedVisit.Contract;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;

namespace Diten.CrmService.Application.Features.PlannedVisit.Handlers.CommandHandlers;

/// <summary>
/// MOD-0155 FU01 shared write-path guards used by both create and update: target resolution (existence + type match +
/// the DERIVED navigation copies AccountId / ContactId / AccountContactLinkId, never client-supplied — V4) and the
/// optional campaign context check. Kept in ONE place so the two write paths cannot drift.
/// <para>These are READ-only lookups against MOD-0149 / MOD-0150 / MOD-0165; no aggregate of another module is ever
/// mutated. The clinic↔pharmacy <c>AccountRelationship</c> is deliberately NOT consulted — it is not a precondition for a
/// pharmacy plan (D9).</para>
/// </summary>
public sealed class PlannedVisitWriteGuards
{
    private const string PharmacyAccountType = "pharmacy";

    private readonly IAccountRepository _accounts;
    private readonly IContactRepository _contacts;
    private readonly IAccountContactLinkRepository _links;
    private readonly ICampaignRepository _campaigns;

    public PlannedVisitWriteGuards(
        IAccountRepository accounts,
        IContactRepository contacts,
        IAccountContactLinkRepository links,
        ICampaignRepository campaigns)
    {
        _accounts = accounts;
        _contacts = contacts;
        _links = links;
        _campaigns = campaigns;
    }

    /// <summary>The resolved target: the derived navigation copies. Only ever produced when the target validates.</summary>
    public sealed record ResolvedTarget(Guid? AccountId, Guid? ContactId, Guid? AccountContactLinkId);

    public sealed record TargetResult(PlannedVisitValidation.Failure? Failure, ResolvedTarget? Target);

    public async Task<TargetResult> ResolveTargetAsync(
        Guid tenantId, string targetType, Guid targetId, CancellationToken cancellationToken)
    {
        var type = PlannedVisitTargetType.Normalize(targetType);

        switch (type)
        {
            case PlannedVisitTargetType.Account:
            {
                var account = await _accounts.GetByIdAsync(tenantId, targetId, cancellationToken);
                if (account is not null)
                {
                    return Ok(new ResolvedTarget(targetId, null, null));
                }

                return await MismatchOrNotFoundAsync(tenantId, targetId, isAccountExpected: true, cancellationToken);
            }

            case PlannedVisitTargetType.Pharmacy:
            {
                var account = await _accounts.GetByIdAsync(tenantId, targetId, cancellationToken);
                if (account is null)
                {
                    return await MismatchOrNotFoundAsync(tenantId, targetId, isAccountExpected: true, cancellationToken);
                }

                // A pharmacy is an Account whose account-type is `pharmacy` (D9). Any other type is a mismatch.
                return string.Equals(account.AccountType?.Trim(), PharmacyAccountType, StringComparison.OrdinalIgnoreCase)
                    ? Ok(new ResolvedTarget(targetId, null, null))
                    : Fail(
                        "The target account is not a pharmacy (account-type must be 'pharmacy').",
                        PlannedVisitErrorCodes.TargetTypeMismatch);
            }

            case PlannedVisitTargetType.Contact:
            {
                var contact = await _contacts.GetByIdAsync(tenantId, targetId, cancellationToken);
                if (contact is not null)
                {
                    return Ok(new ResolvedTarget(null, targetId, null));
                }

                return await MismatchOrNotFoundAsync(tenantId, targetId, isAccountExpected: false, cancellationToken);
            }

            case PlannedVisitTargetType.AccountContactLink:
            {
                var link = await _links.GetByIdAsync(tenantId, targetId, cancellationToken);
                return link is not null
                    ? Ok(new ResolvedTarget(link.AccountId, link.ContactId, targetId))
                    : Fail("The target account-contact link was not found.", PlannedVisitErrorCodes.TargetNotFound);
            }

            default:
                return Fail(
                    $"Unsupported TargetType '{targetType}'.", PlannedVisitErrorCodes.UnsupportedVocabularyValue);
        }
    }

    /// <summary>Optional campaign existence (V16): a supplied CampaignId must name a real campaign — only its existence,
    /// no target/cycle check.</summary>
    public async Task<PlannedVisitValidation.Failure?> ValidateCampaignAsync(
        Guid tenantId, Guid? campaignId, CancellationToken cancellationToken)
    {
        if (campaignId is not { } id || id == Guid.Empty)
        {
            return null;
        }

        var campaign = await _campaigns.GetByIdAsync(tenantId, id, cancellationToken);
        return campaign is not null
            ? null
            : new PlannedVisitValidation.Failure(
                "The referenced campaign was not found.", PlannedVisitErrorCodes.CampaignNotFound);
    }

    private async Task<TargetResult> MismatchOrNotFoundAsync(
        Guid tenantId, Guid targetId, bool isAccountExpected, CancellationToken cancellationToken)
    {
        // The id exists but under the OTHER type → mismatch (AC-TARGET-2); otherwise it simply does not exist.
        if (isAccountExpected)
        {
            var asContact = await _contacts.GetByIdAsync(tenantId, targetId, cancellationToken);
            return asContact is not null
                ? Fail("The target id is a contact, not an account.", PlannedVisitErrorCodes.TargetTypeMismatch)
                : Fail("The target was not found.", PlannedVisitErrorCodes.TargetNotFound);
        }

        var asAccount = await _accounts.GetByIdAsync(tenantId, targetId, cancellationToken);
        return asAccount is not null
            ? Fail("The target id is an account, not a contact.", PlannedVisitErrorCodes.TargetTypeMismatch)
            : Fail("The target was not found.", PlannedVisitErrorCodes.TargetNotFound);
    }

    private static TargetResult Ok(ResolvedTarget target) => new(null, target);

    private static TargetResult Fail(string message, string code)
        => new(new PlannedVisitValidation.Failure(message, code), null);
}
