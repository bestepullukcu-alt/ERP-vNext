namespace Diten.InventoryService.Infrastructure.InternalAccess;

/// <summary>
/// Service-to-service keys of InventoryService's internal operations (WP-0173-PM2, INVENTORY-BUNDLE 1.1.0 "Security
/// conditions"). Section <c>Inventory:InternalServiceKeys</c> — a LIST, one entry per calling service:
/// <code>
/// { "Identifier": "ditenmdmservice", "Secrets": [ "&lt;current&gt;", "&lt;previous, during rotation&gt;" ], "Endpoints": [ "getStockPresence" ] }
/// </code>
/// <list type="bullet">
/// <item>The secrets live ONLY in secure configuration (development: user secrets written by
/// <c>scripts/new-inventory-internal-key.ps1</c>; elsewhere: the environment administrator's setting, e.g.
/// <c>Inventory__InternalServiceKeys__0__Secrets__0</c>). <c>appsettings.json</c> carries an EMPTY list: no key → every call 401.</item>
/// <item>Its own key pair — never the Platform internal-event key (<c>InternalApi:Key</c>), which this section does not read.</item>
/// <item><see cref="Endpoints"/> lists the operation ids the key may open; a key opens nothing else.</item>
/// <item>Rotation: at most <see cref="MaxSecrets"/> valid secrets per identifier.</item>
/// </list>
/// An entry that breaks a rule is IGNORED (that key opens nothing) and logged by identifier and reason — never by secret.
/// </summary>
public sealed class InternalServiceKeyOptions
{
    public const string SectionName = "Inventory:InternalServiceKeys";

    public const int MaxSecrets = 2;

    /// <summary>A secret shorter than this is refused (the dev script writes 43 characters: 32 random bytes, base64url).</summary>
    public const int MinSecretLength = 32;

    public string Identifier { get; set; } = string.Empty;

    public List<string> Secrets { get; set; } = [];

    public List<string> Endpoints { get; set; } = [];
}
