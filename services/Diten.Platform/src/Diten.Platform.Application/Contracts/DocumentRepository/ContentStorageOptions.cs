namespace Diten.Platform.Application.Contracts.DocumentRepository;

/// <summary>
/// MOD-0262-FU01 — document repository storage configuration.
/// <para>
/// Relocated from the MOD-0029 feature models file together with the seam (AD-8 / OD-A). <see cref="SectionName"/>
/// is intentionally unchanged so that no environment's <c>appsettings</c> has to be edited by this follow-up.
/// </para>
/// <para>
/// <b>CT ruling (2026-09-07):</b> <see cref="MaxFileSizeBytes"/> is config-driven with a default greater than
/// 50 MB. The old 50 MB ceiling was a symptom of buffered uploads; AD-4's streaming contract removes that
/// memory constraint. The exact per-environment value is set at deployment time.
/// </para>
/// </summary>
public sealed class ContentStorageOptions
{
    public const string SectionName = "DocumentManagement:ContentStorage";

    public string Provider { get; set; } = "local-filesystem";

    /// <summary>Storage root. MUST NOT be under <c>wwwroot</c> and is never exposed by a public/static URL.</summary>
    public string RootPath { get; set; } = string.Empty;

    /// <summary>Default 256 MB (was 50 MB pre-FU01). Enforced while streaming, not by pre-buffering.</summary>
    public long MaxFileSizeBytes { get; set; } = 268_435_456;

    public List<string> AllowedExtensions { get; set; } =
        [".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".csv", ".md", ".png", ".jpg", ".jpeg"];

    public List<string> AllowedMediaTypes { get; set; } = [];

    /// <summary>
    /// WP-VW-W3-PLT — per-scope file rules. Only <see cref="ContentStorageScope.CrmVisitEvidence"/> has one; every
    /// other scope stays on the general rule above. Configured at
    /// <c>DocumentManagement:ContentStorage:Scopes:CrmVisitEvidence:MaxFileSizeBytes</c>.
    /// </summary>
    public ContentStorageScopeRules Scopes { get; set; } = new();
}

/// <summary>WP-VW-W3-PLT — the scopes that carry their own file rule. A typed property per scope (not a dictionary)
/// so configuration binding fills the defaults in place and an unconfigured environment still gets the rule.</summary>
public sealed class ContentStorageScopeRules
{
    public CrmVisitEvidenceFileRule CrmVisitEvidence { get; set; } = new();

    /// <summary>The rule for <paramref name="scope"/>, or <c>null</c> when the scope uses the general rule.</summary>
    public IContentStorageFileRule? For(ContentStorageScope scope) => scope switch
    {
        ContentStorageScope.CrmVisitEvidence => CrmVisitEvidence,
        _ => null
    };
}

/// <summary>WP-VW-W3-PLT — a scope's own file rule. Replaces the general extension/media-type/size rule for that
/// scope; it is enforced by the gateway exactly like the general one (size while streaming, AD-4).</summary>
public interface IContentStorageFileRule
{
    IReadOnlyList<string> AllowedExtensions { get; }

    IReadOnlyList<string> AllowedMediaTypes { get; }

    long MaxFileSizeBytes { get; }
}

/// <summary>
/// WP-VW-W3-PLT (§2.1) — visit evidence is a photo or a signature image: <c>.jpg</c>/<c>.jpeg</c>/<c>.png</c>
/// declared as <c>image/jpeg</c>/<c>image/png</c>, at most 10 MB. The type lists are the contract and are not
/// configurable (a bound list would be appended to, not replaced); only the size is.
/// </summary>
public sealed class CrmVisitEvidenceFileRule : IContentStorageFileRule
{
    public const long DefaultMaxFileSizeBytes = 10_485_760;

    private static readonly string[] Extensions = [".jpg", ".jpeg", ".png"];
    private static readonly string[] MediaTypes = ["image/jpeg", "image/png"];

    public long MaxFileSizeBytes { get; set; } = DefaultMaxFileSizeBytes;

    IReadOnlyList<string> IContentStorageFileRule.AllowedExtensions => Extensions;

    IReadOnlyList<string> IContentStorageFileRule.AllowedMediaTypes => MediaTypes;
}
