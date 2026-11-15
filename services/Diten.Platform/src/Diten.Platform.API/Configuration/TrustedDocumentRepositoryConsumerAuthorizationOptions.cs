namespace Diten.Platform.API.Configuration;

/// <summary>
/// MOD-0262-FU01 DL-2 — the CLOSED list of trusted document repository consumers (§8.1). Each entry is checked at start-up
/// against a set fixed in code (<c>TrustedDocumentRepositoryConsumerAuthorizationOptionsValidator</c>); configuration alone
/// cannot add a consumer. An empty list is valid and keeps the surface closed (every valid token → 503).
/// <para>No secret lives here: only the Auth-issued client id (the service token's <c>sub</c>).</para>
/// </summary>
public sealed class TrustedDocumentRepositoryConsumerAuthorizationOptions
{
    public const string SectionName = "TrustedDocumentRepositoryConsumerAuthorization";
    public const int MaximumEntries = 8;

    public List<TrustedDocumentRepositoryConsumerEntry> Entries { get; set; } = [];
}

/// <summary>One consumer module's entry. One entry per module, one module per client id.</summary>
public sealed class TrustedDocumentRepositoryConsumerEntry
{
    public Guid ClientId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string ConsumerModule { get; set; } = string.Empty;
    public List<string> Scopes { get; set; } = [];
}
