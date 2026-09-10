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
}
