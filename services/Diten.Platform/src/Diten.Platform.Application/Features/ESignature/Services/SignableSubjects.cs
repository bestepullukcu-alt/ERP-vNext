using System.Text.RegularExpressions;

namespace Diten.Platform.Application.Features.ESignature.Services;

/// <summary>
/// MOD-0022 — what a subject provider answers about one record (ESIGN-BUNDLE §2.2): the display line and summary the signer
/// sees, the record's company (KS-2, never from the client), and the governance <c>fields</c> the subject hash is computed
/// over. The provider OWNS the field list (§6): it must keep names stable, or old signatures verify as <c>changed</c>.
/// </summary>
public sealed record SignableSubject(
    string Display,
    IReadOnlyList<SignableSubjectLine> Summary,
    Guid? LegalEntityId,
    IReadOnlyDictionary<string, string?> Fields);

public sealed record SignableSubjectLine(string Label, string Value);

public enum SignableSubjectOutcome
{
    Found = 0,

    /// <summary>Unknown, another tenant's, or not visible to the caller — one answer, no oracle (404).</summary>
    NotFound = 1,

    /// <summary>The owner could not be asked (remote provider down / misconfigured). Signing fails closed.</summary>
    Unavailable = 2
}

public sealed record SignableSubjectResolution(SignableSubjectOutcome Outcome, SignableSubject? Subject)
{
    public static SignableSubjectResolution Found(SignableSubject subject) => new(SignableSubjectOutcome.Found, subject);
    public static readonly SignableSubjectResolution NotFound = new(SignableSubjectOutcome.NotFound, null);
    public static readonly SignableSubjectResolution Unavailable = new(SignableSubjectOutcome.Unavailable, null);
}

/// <summary>
/// An in-process provider for one consumer module (CT-D2). It is called inside the caller's request, so the tenant context
/// and the caller's visibility apply exactly as for the module's own reads. A provider never throws for "not found".
/// </summary>
public interface ISignableSubjectProvider
{
    /// <summary>The consumer module code, e.g. <c>document-management</c>.</summary>
    string Module { get; }

    Task<SignableSubjectResolution> ResolveAsync(string subjectType, string subjectId, CancellationToken ct);
}

/// <summary>
/// A provider in another service, reached over HTTP with the CALLER's token (ESIGN-BUNDLE §6). The base URL comes from
/// operator configuration (<c>ESign:SubjectProviders:{module}:BaseUrl</c>) — never from a manifest (WC-D1 Y2) — and one
/// generic implementation serves every remote module (Y1).
/// </summary>
public interface IRemoteSignableSubjectClient
{
    bool IsConfigured(string module);

    Task<SignableSubjectResolution> ResolveAsync(string module, string subjectType, string subjectId, CancellationToken ct);
}

/// <summary>The registry: in-process provider for the module if there is one, else the configured remote owner, else 404.</summary>
public interface ISignableSubjectRegistry
{
    Task<SignableSubjectResolution> ResolveAsync(string module, string subjectType, string subjectId, CancellationToken ct);
}

public sealed class SignableSubjectRegistry : ISignableSubjectRegistry
{
    // ESIGN-BUNDLE §2.1 / §2.2 limits.
    private static readonly Regex ModulePattern = new("^[a-z][a-z0-9-]{1,40}$", RegexOptions.Compiled);
    private static readonly Regex SubjectTypePattern = new("^[a-z][a-z0-9.-]{1,80}$", RegexOptions.Compiled);
    private const int MaxSummaryLines = 20;
    private const int MaxFields = 100;
    private const int MaxDisplay = 300;

    private readonly IReadOnlyDictionary<string, ISignableSubjectProvider> _providers;
    private readonly IRemoteSignableSubjectClient _remote;

    public SignableSubjectRegistry(IEnumerable<ISignableSubjectProvider> providers, IRemoteSignableSubjectClient remote)
    {
        _providers = providers.ToDictionary(p => p.Module, StringComparer.Ordinal);
        _remote = remote;
    }

    public static bool IsValidRef(string? module, string? subjectType, string? subjectId) =>
        module is not null && ModulePattern.IsMatch(module)
        && subjectType is not null && SubjectTypePattern.IsMatch(subjectType)
        && !string.IsNullOrWhiteSpace(subjectId) && subjectId.Length <= 100;

    public async Task<SignableSubjectResolution> ResolveAsync(string module, string subjectType, string subjectId, CancellationToken ct)
    {
        if (!IsValidRef(module, subjectType, subjectId))
        {
            return SignableSubjectResolution.NotFound;
        }

        SignableSubjectResolution resolution;
        if (_providers.TryGetValue(module, out var provider))
        {
            resolution = await provider.ResolveAsync(subjectType, subjectId, ct);
        }
        else if (_remote.IsConfigured(module))
        {
            resolution = await _remote.ResolveAsync(module, subjectType, subjectId, ct);
        }
        else
        {
            return SignableSubjectResolution.NotFound;
        }

        return resolution.Outcome == SignableSubjectOutcome.Found ? Bounded(resolution.Subject!) : resolution;
    }

    /// <summary>A provider answer outside the §2.2 limits is not trusted: treated as unavailable, never truncated silently.</summary>
    private static SignableSubjectResolution Bounded(SignableSubject subject)
    {
        if (string.IsNullOrWhiteSpace(subject.Display)
            || subject.Display.Length > MaxDisplay
            || subject.Summary.Count > MaxSummaryLines
            || subject.Summary.Any(l => l.Label.Length > 60 || l.Value.Length > 300)
            || subject.Fields.Count > MaxFields)
        {
            return SignableSubjectResolution.Unavailable;
        }

        return SignableSubjectResolution.Found(subject);
    }
}
