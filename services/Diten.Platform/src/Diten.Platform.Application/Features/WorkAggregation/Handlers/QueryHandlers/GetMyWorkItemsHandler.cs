using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.WorkAggregation.Providers;
using Diten.Platform.Application.Features.WorkAggregation.Queries;
using Diten.Platform.Application.Features.WorkAggregation.Services;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Application.Features.WorkAggregation.Handlers.QueryHandlers;

// WC-1 (DCP-004) — aggregates every bound provider's work items for the current user into
// one canonical list. The handler iterates an IEnumerable<IWorkItemProvider> so a new provider is added
// additively (MOD-0023 approvals and MOD-0024 tasks are bound today) without touching this handler.
//
// READ-ONLY: no state is written. UserId is resolved server-side (never from the client payload).
//
// ── WC-D3 (DCP-004 §2 D3) — ONE PROVIDER'S BAD DAY IS NOT THE BOARD'S ─────────────────────────────────────
//
// What this loop used to be, measured and written up in DCP-004 §2 D3:
//
//     foreach (var provider in _providers)
//         var items = await provider.GetWorkItemsAsync(actor, ct);   // no try, no timeout, no partial result
//
// One provider throwing propagated out of the handler and the reader got an empty error page instead of the
// rows the OTHER provider had ready. One provider hanging hung the request, because there is no timeout under
// this path either: Platform API configures no request timeout, and the gateway has no QoSOptions on any of its
// 110 routes. Both providers are in-process Mongo reads today, so neither showed — and the first
// network-backed provider is the first one that can be slow or absent.
//
// Now each provider runs inside its OWN try and its OWN linked timeout, and what did not answer is REPORTED
// rather than quietly missing (see WorkItemBoardDto). Three rules hold here:
//
//   1. A provider's failure or timeout cannot reach another provider. The isolation is per call, not per loop.
//   2. A partial board is never silent. Every source that dropped out is named, with a stable reason CODE.
//   3. The caller's OWN cancellation is not a provider fault. If the reader navigated away, the request is
//      abandoned — inventing a partial board for a caller who is gone would report a failure that never
//      happened.
//
// STILL SEQUENTIAL, on purpose. Providers are registered Scoped; calling them concurrently would share one DI
// scope (and its Mongo session) across threads, which is a separate decision with a separate hazard. The cost
// is that the worst case is N × ProviderTimeout — recorded as BL-303, to be revisited when the provider count
// grows.
public sealed class GetMyWorkItemsHandler
    : IRequestHandler<GetMyWorkItemsQuery, Response<WorkItemBoardDto>>
{
    // Provider contract versions this projection generation can map. An unknown version is not projected —
    // and, since WC-D3, is not silent either: it is reported as UNSUPPORTED_VERSION.
    private static readonly HashSet<string> SupportedProviderContractVersions = ["1.0"];

    private readonly IEnumerable<IWorkItemProvider> _providers;
    private readonly ICurrentUserContext _currentUser;
    private readonly WorkAggregationResilienceOptions _resilience;
    private readonly ILogger<GetMyWorkItemsHandler> _logger;

    public GetMyWorkItemsHandler(
        IEnumerable<IWorkItemProvider> providers,
        ICurrentUserContext currentUser,
        IOptions<WorkAggregationResilienceOptions> resilience,
        ILogger<GetMyWorkItemsHandler> logger)
    {
        _providers = providers;
        _currentUser = currentUser;
        _resilience = resilience.Value;
        _logger = logger;
    }

    public async Task<Response<WorkItemBoardDto>> Handle(
        GetMyWorkItemsQuery request,
        CancellationToken ct)
    {
        var actor = new WorkItemActor(
            UserId: _currentUser.UserId,
            IsPlatformActor: request.IsPlatformActor,
            GrantedPermissions: request.GrantedPermissions)
        {
            // BL-023 — a provider with no team concept ignores this; the Tasks provider honours it.
            Scope = request.Scope
        };

        var aggregated = new List<WorkItemProjectionDto>();
        var unavailable = new List<WorkItemUnavailableSourceDto>();

        foreach (var provider in _providers)
        {
            // Charter OD-WC-04 — contract-version handshake. An unmappable provider is still not projected (a
            // mis-projected item is worse than a missing one), but it is now NAMED. The bare `continue` this
            // replaces was the same defect as the missing try/catch below, only smaller: a source leaving the
            // board while the board looked whole.
            if (!SupportedProviderContractVersions.Contains(provider.ProviderContractVersion))
            {
                _logger.LogWarning(
                    "Work aggregation skipped provider {ProviderCode}: unsupported contract version {Version}.",
                    provider.ProviderCode,
                    provider.ProviderContractVersion);
                unavailable.Add(new WorkItemUnavailableSourceDto(
                    provider.ProviderCode,
                    WorkAggregationUnavailableReasonCodes.UnsupportedVersion));
                continue;
            }

            // The budget is this provider's alone. Linked to the request token so a caller who goes away still
            // cancels everything in flight, and separate per iteration so a spent budget cannot leak into the
            // next provider's call.
            using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
            if (_resilience.ProviderTimeout <= TimeSpan.Zero)
            {
                // "No time allowed" is answered exactly, not approximately. CancelAfter(0) would leave a race
                // between the timer and a fast provider; cancelling here makes a zero budget deterministic,
                // which is what the timeout guard test stands on (and why it costs no wall-clock time).
                budget.Cancel();
            }
            else
            {
                budget.CancelAfter(_resilience.ProviderTimeout);
            }

            try
            {
                var items = await provider.GetWorkItemsAsync(actor, budget.Token);
                aggregated.AddRange(items);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // The CALLER cancelled (navigated away, connection dropped). Not this provider's failure and not
                // a partial board — there is nobody left to show one to.
                throw;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning(
                    "Work aggregation provider {ProviderCode} exceeded its {Timeout} budget; board is partial.",
                    provider.ProviderCode,
                    _resilience.ProviderTimeout);
                unavailable.Add(new WorkItemUnavailableSourceDto(
                    provider.ProviderCode,
                    WorkAggregationUnavailableReasonCodes.Timeout));
            }
            catch (Exception ex)
            {
                // Deliberately broad: this seam exists so that whatever a provider does wrong stops here. A
                // narrower filter would let the next unforeseen exception type empty the board again, which is
                // the exact failure being closed.
                _logger.LogError(
                    ex,
                    "Work aggregation provider {ProviderCode} failed; board is partial.",
                    provider.ProviderCode);
                unavailable.Add(new WorkItemUnavailableSourceDto(
                    provider.ProviderCode,
                    WorkAggregationUnavailableReasonCodes.Error));
            }
        }

        IReadOnlyList<WorkItemProjectionDto> ordered = aggregated
            .OrderBy(i => i.DueAt ?? DateTimeOffset.MaxValue)
            .ToList();

        var board = new WorkItemBoardDto(ordered, unavailable);
        return Response<WorkItemBoardDto>.Success(board, correlationId: request.CorrelationId);
    }
}
