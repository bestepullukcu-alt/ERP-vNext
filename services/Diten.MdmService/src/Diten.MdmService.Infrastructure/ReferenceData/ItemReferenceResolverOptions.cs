namespace Diten.MdmService.Infrastructure.ReferenceData;

/// <summary>
/// WP-PLT-ITEM-LISTS-01 (CT decision A): the item lists use the existing MDM resolver configuration
/// (<see cref="VerifiedGskuResolverOptions"/>, section "VerifiedGskuResolver"); there is no item-specific switch and no
/// start refusal. Without that configuration every item create / update answers 503 (fail-closed at the call) and the
/// start says so once outside Development.
/// </summary>
public static class ItemReferenceProvider
{
    public const string NotConfiguredWarning =
        "Item reference provider not configured — item create/update answer 503 (VerifiedGskuResolver section missing or invalid).";
}
