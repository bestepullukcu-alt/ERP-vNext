using System.Text.Json;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Application.Features.WorkAggregation.Providers;
using Diten.Platform.Infrastructure.Settings;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Infrastructure.Services.WorkAggregation;

/// <summary>
/// WC-D1 (DCP-004 §2 D1) — THE READ HALF OF THE GENERAL BRIDGE. One class; one instance per configuration row.
///
/// <para><b>The rule this class exists to enforce: a module gets a ROW, not a class.</b> The onboarding note has
/// said since 2026-08-24 that "a module running as its own service cannot register into that container … there is
/// no precedent for this yet; the first module to need it is writing the pattern". The pattern is this file, and
/// the reason it is one file is a measured hazard rather than tidiness: if every team writes its own bridge into
/// Platform, the repository holds N teams' error handling and N teams' timeouts, one module slows the board, and
/// nobody can say which. That turns a discipline problem into an architecture problem.</para>
///
/// <para><b>What a module has to do</b> is open ONE endpoint —
/// <c>GET {BaseUrl}/{ProjectionPath}</c>, by default <c>api/v1/work-items/projection</c> — that answers the
/// shared <c>Response&lt;T&gt;</c> envelope wrapping <c>{ contractVersion, items[] }</c>, where each item is the
/// canonical <see cref="WorkItemProjectionDto"/>. The full contract, field by field, is
/// <c>DCP-004-provider-onboarding-note.md</c> §7.</para>
///
/// <para><b>Failure is the aggregation loop's to report, not this class's to hide.</b> WC-D3 built exactly the
/// machinery this needs — a per-provider budget, a per-provider <c>try</c>, and a named entry in
/// <c>UnavailableSources</c> — and said in its own words that "the first network-backed provider is the first one
/// that can be slow or absent". This is that provider, and it is D3's first real customer. So an unreachable
/// module THROWS here and is reported as <c>ERROR</c>; an exceeded budget surfaces as the cancellation the loop
/// catches and reports as <c>TIMEOUT</c>. Neither is swallowed into an empty list, because an empty list is
/// indistinguishable from "you have no work" — which is the one thing a board must never say by accident.</para>
/// </summary>
public sealed class HttpWorkItemProvider : IWorkItemProvider
{
    private readonly RemoteWorkItemProviderOptions _row;
    private readonly RemoteWorkItemGateway _gateway;
    private readonly ILogger<HttpWorkItemProvider> _logger;

    public HttpWorkItemProvider(
        RemoteWorkItemProviderOptions row,
        RemoteWorkItemGateway gateway,
        ILogger<HttpWorkItemProvider> logger)
    {
        _row = row;
        _gateway = gateway;
        _logger = logger;
    }

    public string ProviderCode => _row.ProviderCode;

    public string ProviderContractVersion => _row.ContractVersion;

    /// <summary>
    /// Read straight off the row's action map, which is the SAME map the dispatcher answers
    /// <c>RequiredPermission</c> from. The onboarding note's §3 trap — a key consulted but not declared, so
    /// <c>actor.Has</c> silently answers false and a permitted caller is shown PERMISSION_DENIED — is not
    /// avoidable-by-discipline here; it is unreachable, because there is only one list.
    /// </summary>
    public IReadOnlyCollection<string> RequiredActionPermissions
        => _row.Actions.Values.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    public async Task<IReadOnlyList<WorkItemProjectionDto>> GetWorkItemsAsync(
        WorkItemActor actor,
        CancellationToken ct = default)
    {
        // No deadline is imposed here on purpose: `ct` already carries this provider's budget from
        // GetMyWorkItemsHandler (WorkAggregation:Resilience:ProviderTimeout). A second one would be a second
        // operator answer to a single operator question.
        var outcome = await _gateway.SendAsync(
            _row,
            HttpMethod.Get,
            $"{_row.ProjectionPath}?scope={ScopeParameter(actor.Scope)}",
            body: null,
            correlationId: null,
            ct);

        if (!outcome.Reached)
        {
            // THROW rather than return nothing. The loop turns this into a named ERROR source and the reader sees
            // a warning strip over a partial board; returning [] would draw a complete-looking board with this
            // module's work silently absent.
            throw new HttpRequestException(
                $"Work-item provider '{_row.ProviderCode}' could not be reached: {outcome.FailureDetail}");
        }

        if (!outcome.Succeeded)
        {
            throw new HttpRequestException(
                $"Work-item provider '{_row.ProviderCode}' answered {outcome.StatusCode}"
                + $" ({outcome.ReasonCode ?? "no reason code"}).");
        }

        var payload = Deserialize(outcome.Data);
        if (payload is null)
        {
            throw new HttpRequestException(
                $"Work-item provider '{_row.ProviderCode}' answered a body that is not a projection.");
        }

        /*
         * The version is checked in BOTH directions and a disagreement is a failure, not a guess.
         *
         * The handler already decided to call this provider using the row's declared ContractVersion — the only
         * version available before a call exists. If the module then answers with a different generation, the row
         * is stale, and projecting its items anyway would map a shape nobody has agreed to. A mis-projected item
         * is worse than a missing one (charter OD-WC-04); this reports the source as unavailable instead.
         */
        if (!string.IsNullOrWhiteSpace(payload.ContractVersion)
            && !string.Equals(payload.ContractVersion, _row.ContractVersion, StringComparison.OrdinalIgnoreCase))
        {
            throw new HttpRequestException(
                $"Work-item provider '{_row.ProviderCode}' answered contract version '{payload.ContractVersion}'"
                + $" but is configured as '{_row.ContractVersion}'.");
        }

        var items = new List<WorkItemProjectionDto>();
        foreach (var item in payload.Items ?? [])
        {
            if (item is null)
            {
                continue;
            }

            /*
             * A MODULE MAY NOT PROJECT ON ANOTHER MODULE'S BEHALF.
             *
             * `source.providerCode` is what the browser posts back as the write address, so an item claiming a
             * code this row was not configured for would route the next click at a different module — a redirect
             * chosen by the party being called. That is the same hazard as taking an address from a manifest (D1),
             * one level down, and it is refused for the same reason.
             */
            if (!string.Equals(item.Source?.ProviderCode, _row.ProviderCode, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Work-item provider {ProviderCode} returned an item whose source claims {ClaimedCode}; dropped.",
                    _row.ProviderCode,
                    item.Source?.ProviderCode ?? "(none)");
                continue;
            }

            items.Add(GateActions(item, actor));
        }

        return items;
    }

    /// <summary>
    /// PERMISSION AND DISPATCHABILITY ARE DECIDED HERE, over whatever the module said.
    ///
    /// <para>Two things the module is not authority for. First, what the caller may do: the granted set is
    /// evaluated from the caller's CLAIMS on this side, and a remote <c>enabled: true</c> on an action the caller
    /// has no permission for is downgraded to a disabled action with <c>PERMISSION_DENIED</c> — the same code and
    /// the same treatment an in-process provider's action gets. Second, whether a button can work at all: an
    /// action absent from this row's map has no permission to check and no dispatch path behind it, so it is
    /// REMOVED rather than drawn. A drawn button that reaches nothing is precisely the defect DCP-004 §2 D2
    /// records, and adding a network hop is no reason to re-ship it.</para>
    /// </summary>
    private WorkItemProjectionDto GateActions(WorkItemProjectionDto item, WorkItemActor actor)
    {
        var actions = new List<WorkItemActionDto>();
        var dropped = new List<string>();

        foreach (var action in item.Actions ?? [])
        {
            if (action is null)
            {
                continue;
            }

            if (!_row.Actions.TryGetValue(action.Code, out var permission))
            {
                dropped.Add(action.Code);
                continue;
            }

            actions.Add(actor.Has(permission)
                ? action
                : action with
                {
                    Enabled = false,
                    DisabledReasonCode = WorkAggregationReasonCodes.PermissionDenied,
                    DisabledReason = action.DisabledReason
                });
        }

        if (dropped.Count > 0)
        {
            _logger.LogWarning(
                "Work-item provider {ProviderCode} projected {Count} action(s) with no configured permission "
                + "({Codes}); they are not offered. Add them to '{Section}' Actions to enable them.",
                _row.ProviderCode,
                dropped.Count,
                string.Join(", ", dropped),
                RemoteWorkItemProviderOptions.SectionName);
        }

        // PrimaryActionCode / OverflowActionCodes must not point at an action that is no longer in the list — the
        // executable contract rejects a dangling reference and the whole item would be dropped by the client.
        var codes = actions.Select(a => a.Code).ToHashSet(StringComparer.Ordinal);
        var primary = item.PrimaryActionCode is not null && codes.Contains(item.PrimaryActionCode)
            ? item.PrimaryActionCode
            : null;
        var overflow = item.OverflowActionCodes?.Where(codes.Contains).ToList();

        return item with
        {
            Actions = actions,
            PrimaryActionCode = primary,
            OverflowActionCodes = overflow is { Count: > 0 } ? overflow : null
        };
    }

    private static string ScopeParameter(WorkItemScope scope)
        => scope == WorkItemScope.Team ? "team" : "self";

    private static RemoteWorkItemProjectionPayload? Deserialize(JsonElement? data)
    {
        if (data is not { ValueKind: JsonValueKind.Object } element)
        {
            return null;
        }

        try
        {
            return element.Deserialize<RemoteWorkItemProjectionPayload>(RemoteWorkItemGateway.Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>
/// The <c>data</c> a module's projection endpoint carries. Deliberately an OBJECT and not a bare array: the
/// version handshake needs somewhere to live, and a bare array leaves no room to add one later without breaking
/// every module at once.
/// </summary>
public sealed record RemoteWorkItemProjectionPayload(
    string? ContractVersion,
    IReadOnlyList<WorkItemProjectionDto?>? Items);
