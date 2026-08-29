namespace Diten.CrmService.Application.Features.AccountRelationship;

public sealed record AccountRelationshipDto(
    Guid Id,
    Guid SourceAccountId,
    Guid TargetAccountId,
    string RelationshipType,
    string Direction,
    string Status,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidTo,
    string? Notes,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

/// <summary>
/// Account 360 "Related Accounts" projection row. <see cref="DisplayDirection"/> is "direct" when the queried account
/// is the source, "inverse" when it is the target of a directional type, or "bidirectional". <see cref="EffectiveLabelCode"/>
/// is the code to render from THIS account's perspective (RelationshipType for direct/bidirectional, InverseLabelCode for inverse).
/// </summary>
public sealed record RelatedAccountDto(
    Guid AccountRelationshipId,
    Guid SourceAccountId,
    Guid TargetAccountId,
    Guid RelatedAccountId,
    string RelatedAccountName,
    string RelatedAccountCode,
    string RelatedAccountType,
    string RelationshipType,
    string? InverseLabelCode,
    string DisplayDirection,
    string EffectiveLabelCode,
    string Status,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidTo,
    string? Notes);

/// <summary>Parsed account-relationship-type metadata (from the MOD-0048 published value attributes).</summary>
public sealed record RelationshipTypeMetadata(bool IsBidirectional, string? InverseLabelCode, bool SelfAllowed)
{
    public static RelationshipTypeMetadata Parse(IReadOnlyDictionary<string, string>? attributes)
    {
        if (attributes is null)
        {
            // Degraded mode (metadata absent): treat as directional, no inverse label, self not allowed.
            return new RelationshipTypeMetadata(false, null, false);
        }

        var direction = attributes.TryGetValue("direction", out var d) ? d : "directional";
        var inverse = attributes.TryGetValue("inverseLabelCode", out var i) && !string.IsNullOrWhiteSpace(i) ? i : null;
        var selfAllowed = attributes.TryGetValue("selfAllowed", out var s)
            && bool.TryParse(s, out var b) && b;
        return new RelationshipTypeMetadata(
            string.Equals(direction, "bidirectional", StringComparison.OrdinalIgnoreCase), inverse, selfAllowed);
    }

    public string DirectionCode => IsBidirectional ? "bidirectional" : "outbound";
}
