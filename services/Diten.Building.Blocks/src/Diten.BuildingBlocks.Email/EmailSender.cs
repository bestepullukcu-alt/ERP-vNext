namespace Diten.BuildingBlocks.Email;

/// <summary>
/// BL-454 — the ONE rule for the name a system e-mail is sent under, used by both AuthService and Platform.
///
/// <list type="number">
/// <item>The tenant wrote a sender name in its OWN messaging settings → that name, as written. It is a deliberate
/// statement and nothing is appended to it.</item>
/// <item>Otherwise, a tenant is behind the message → <c>"{tenant display name} (via Di10)"</c>, the "via" in the
/// reader's language. The address is the platform's, so the name says whose message it is and who carried it.</item>
/// <item>No tenant (a platform e-mail) → the product's one name.</item>
/// </list>
///
/// <para>The platform-default settings row's sender name is NOT rule 1 — it is the platform's, and reading it as a
/// tenant's would put the same name on every tenant's mail.</para>
/// </summary>
public static class EmailSender
{
    public static string ComposeDisplayName(string? tenantOwnSenderName, string? tenantDisplayName, string? language)
    {
        var own = EmailHeaderText.CleanDisplayName(tenantOwnSenderName);
        if (own.Length > 0)
        {
            return own;
        }

        var tenant = EmailHeaderText.CleanDisplayName(tenantDisplayName);
        if (tenant.Length == 0)
        {
            return EmailProduct.Name;
        }

        var texts = EmailShellTexts.For(language);
        var composed = string.Format(texts.SenderVia, tenant, EmailProduct.Name);
        if (composed.Length <= EmailHeaderText.MaxDisplayNameLength)
        {
            return composed;
        }

        // The tenant name gives way, never the "via" part: a cut that dropped it would turn rule 2 into a bare
        // tenant name on the platform's address.
        var overflow = composed.Length - EmailHeaderText.MaxDisplayNameLength;
        var shortened = tenant[..Math.Max(1, tenant.Length - overflow - 1)].TrimEnd() + "…";
        return string.Format(texts.SenderVia, shortened, EmailProduct.Name);
    }
}
