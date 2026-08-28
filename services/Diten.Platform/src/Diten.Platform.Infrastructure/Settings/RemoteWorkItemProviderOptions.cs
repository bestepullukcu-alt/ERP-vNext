namespace Diten.Platform.Infrastructure.Settings;

/// <summary>
/// WC-D1 (DCP-004 §2 D1) — ONE ROW PER MODULE THAT LIVES IN ANOTHER SERVICE.
///
/// <para><b>A new module is a new ROW, never a new class.</b> <c>HttpWorkItemProvider</c> and
/// <c>HttpWorkItemActionDispatcher</c> are registered once per row from this list, so the code that reaches a
/// module over the network exists exactly once — one timeout, one fail-closed rule, one identity propagation,
/// one error dictionary. A per-module bridge class would give N teams N of each, and the first slow module
/// would slow the whole board with nobody able to say which one or why. A guard test refuses a second
/// implementation of either seam outside this file's registration.</para>
///
/// <para><b>The address is the OPERATOR'S, not the module's</b> (owner decision, D1). It is written here by hand,
/// in the same shape as <c>MdmService:BaseUrl</c> and the six other inter-service addresses this repo already
/// keeps in configuration. It is deliberately NOT taken from the self-registration manifest: a manifest is
/// client-supplied, so an address inside it is the party being called telling Platform where to send a caller's
/// JWT — a redirect written by the callee. There is no precedent for that in this repository and this round does
/// not create one.</para>
/// </summary>
public sealed class RemoteWorkItemProviderOptions
{
    /// <summary>The whole list. Bound from <c>WorkAggregation:RemoteProviders</c>.</summary>
    public const string SectionName = "WorkAggregation:RemoteProviders";

    /// <summary>
    /// The provider's stable code — the same string its items carry in <c>source.providerCode</c> and the same
    /// string the browser posts back as <c>providerCode</c>. It is the join between the read half and the write
    /// half, so a row's code and the module's own projection MUST agree; the provider verifies every item it
    /// receives and drops the ones that claim another source (a module may not project on another's behalf).
    /// </summary>
    public string ProviderCode { get; set; } = string.Empty;

    /// <summary>
    /// The projection contract generation this module speaks. Declared HERE and not read from the response,
    /// because <c>GetMyWorkItemsHandler</c> decides whether to call a provider at all from
    /// <c>ProviderContractVersion</c> — before any call exists to read a version out of. The response envelope's
    /// version is still checked against this one, and a disagreement is a failure rather than a guess.
    /// </summary>
    public string ContractVersion { get; set; } = "1.0";

    /// <summary>Scheme, host and port of the module's service. No path.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// The ONE read endpoint a module must open. Overridable per row only so a module that already serves the
    /// shape under a different path is not forced to move it; the default is the contract.
    /// </summary>
    public string ProjectionPath { get; set; } = "api/v1/work-items/projection";

    /// <summary>
    /// The ONE write endpoint, with <c>{itemId}</c> and <c>{actionCode}</c> substituted. Same address shape
    /// Platform itself exposes, on purpose: a module implementing this contract implements one endpoint pair, not
    /// a per-action endpoint list.
    /// </summary>
    public string ActionPathTemplate { get; set; } = "api/v1/work-items/{itemId}/actions/{actionCode}";

    /// <summary>
    /// <c>actionCode → permission key</c>, and the reason both halves of the bridge can be built from one row.
    ///
    /// <para>The provider publishes these keys as its <c>RequiredActionPermissions</c> and the dispatcher names
    /// the same key as the action's <c>RequiredPermission</c>, so the containment the guard test asserts holds by
    /// CONSTRUCTION rather than by two lists being kept in step. The permission trap the onboarding note §3
    /// records — a key consulted but not declared, silently answering false — cannot be walked into from here.</para>
    ///
    /// <para>An action the module projects that is absent from this map is NOT DRAWN: the provider strips it and
    /// logs. That is the fail-closed direction — an undispatchable button is the defect this whole capability
    /// exists to remove, and a missing button is visibly missing while a dead one is not.</para>
    /// </summary>
    public Dictionary<string, string> Actions { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// TRUE marks a row that exists to PROVE the pattern rather than to serve a real module (today: the
    /// DevEnablement reference endpoint). It changes no behaviour — the bridge treats it exactly like any other
    /// row, which is the point — but it is logged at startup and is what a reader greps for when asking "is
    /// anything temporary still wired in production?".
    /// </summary>
    public bool Temporary { get; set; }

    /// <summary>
    /// Everything wrong with this row, in sentences an operator can act on. Empty means usable.
    ///
    /// <para>Called at STARTUP, and a bad row stops the service. A misconfigured address that only surfaced as a
    /// permanently unavailable source on somebody's board would be a silent failure wearing an honest-looking
    /// warning strip: the board would correctly say the source is missing, and nobody would learn that the reason
    /// was a typo committed weeks earlier.</para>
    /// </summary>
    public IReadOnlyList<string> Validate(int index)
    {
        var errors = new List<string>();
        var where = $"'{SectionName}[{index}]'";

        if (string.IsNullOrWhiteSpace(ProviderCode))
        {
            errors.Add($"{where}: 'ProviderCode' is required.");
        }

        if (string.IsNullOrWhiteSpace(ContractVersion))
        {
            errors.Add($"{where}: 'ContractVersion' is required.");
        }

        if (string.IsNullOrWhiteSpace(BaseUrl)
            || !Uri.TryCreate(BaseUrl, UriKind.Absolute, out var baseUri)
            || (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            errors.Add($"{where}: 'BaseUrl' must be an absolute http/https address (got '{BaseUrl}').");
        }

        if (string.IsNullOrWhiteSpace(ProjectionPath))
        {
            errors.Add($"{where}: 'ProjectionPath' is required.");
        }

        if (string.IsNullOrWhiteSpace(ActionPathTemplate)
            || !ActionPathTemplate.Contains("{itemId}", StringComparison.Ordinal)
            || !ActionPathTemplate.Contains("{actionCode}", StringComparison.Ordinal))
        {
            errors.Add($"{where}: 'ActionPathTemplate' must contain both '{{itemId}}' and '{{actionCode}}'.");
        }

        foreach (var (code, permission) in Actions)
        {
            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(permission))
            {
                // A blank permission would be read as "no permission needed" and open a remote write to any
                // authenticated caller. Refusing to start is the only safe reading of a half-written row.
                errors.Add($"{where}: action '{code}' must name a non-empty permission key.");
            }
        }

        return errors;
    }
}
