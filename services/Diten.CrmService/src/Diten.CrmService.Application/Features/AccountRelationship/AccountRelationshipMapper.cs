using DomainRel = Diten.CrmService.Domain.Entities.AccountRelationship;
using DomainAccount = Diten.CrmService.Domain.Entities.Account;

namespace Diten.CrmService.Application.Features.AccountRelationship;

public static class AccountRelationshipMapper
{
    public static AccountRelationshipDto ToDto(DomainRel r)
        => new(r.Id, r.SourceAccountId, r.TargetAccountId, r.RelationshipType, r.Direction, r.Status, r.ValidFrom, r.ValidTo, r.Notes, r.CreatedAt, r.UpdatedAt);

    /// <summary>
    /// Builds the Account 360 row from the queried account's perspective. <paramref name="queriedIsSource"/> decides
    /// direct vs inverse display; <paramref name="metadata"/> supplies the inverse label + bidirectionality.
    /// </summary>
    public static RelatedAccountDto ToRelatedAccount(DomainRel r, DomainAccount related, RelationshipTypeMetadata metadata, bool queriedIsSource)
    {
        string displayDirection;
        string effectiveLabel;

        if (metadata.IsBidirectional)
        {
            displayDirection = "bidirectional";
            effectiveLabel = r.RelationshipType;
        }
        else if (queriedIsSource)
        {
            displayDirection = "direct";
            effectiveLabel = r.RelationshipType;
        }
        else
        {
            displayDirection = "inverse";
            effectiveLabel = metadata.InverseLabelCode ?? r.RelationshipType;
        }

        return new RelatedAccountDto(
            r.Id, r.SourceAccountId, r.TargetAccountId, related.Id,
            related.AccountName, related.AccountCode, related.AccountType,
            r.RelationshipType, metadata.InverseLabelCode, displayDirection, effectiveLabel,
            r.Status, r.ValidFrom, r.ValidTo, r.Notes);
    }
}
