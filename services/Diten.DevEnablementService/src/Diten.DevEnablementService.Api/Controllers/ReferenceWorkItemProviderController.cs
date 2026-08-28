using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace Diten.DevEnablementService.Api.Controllers;

/// <summary>
/// ⚠⚠ TEMPORARY — THE REFERENCE CONSUMER OF THE WORK-ITEM BRIDGE, AND NOTHING ELSE. ⚠⚠
///
/// <para><b>Why it exists.</b> DCP-004 records, twice, that a seam proven on one implementation proves nothing:
/// the read seam was built against a single provider and the projection defects only surfaced when a second one
/// arrived; the dispatch seam deliberately shipped with two dispatchers for the same reason. The HTTP bridge has
/// the same exposure and worse, because it is the first provider that lives across a network — and on the day it
/// was written NO MODULE HAD OPENED THE ENDPOINT YET. Waiting for one would have blocked the round; closing the
/// round with nothing on the far end would have repeated the documented mistake. So the far end is this, and it
/// is marked temporary rather than quietly left to look real.</para>
///
/// <para><b>What it proves, and it is not a mock.</b> Platform reaches a DIFFERENT SERVICE over a real socket,
/// carrying the caller's own bearer token and the tenant header; the item comes back through the canonical
/// projection shape; the button on the board posts to Platform, Platform posts here, the state CHANGES, and the
/// next board read shows the new state. That is the whole chain, end to end, with nothing simulated in between.
/// The item's title carries the tenant this service actually received, so tenant propagation is legible on the
/// screen rather than only in a test.</para>
///
/// <para><b>What it is not.</b> There is no business meaning here and no database: the state lives in a static
/// dictionary and dies with the process. It is OFF unless
/// <c>WorkItemReferenceProvider:Enabled</c> is true, and it must be deleted the day a real module opens its own
/// projection endpoint — recorded as BL-310 so the deletion is somebody's job rather than everybody's assumption.</para>
///
/// <para><b>It is also the executable half of the module-side contract</b> in
/// <c>DCP-004-provider-onboarding-note.md</c> §7. A team writing their own endpoint can read this file for the
/// exact envelope, the exact field names and the exact refusal shape.</para>
/// </summary>
[ApiController]
[Route("api/v1/work-items")]
[Authorize]
public sealed class ReferenceWorkItemProviderController : ControllerBase
{
    /// <summary>The provider code an operator must write in Platform's <c>WorkAggregation:RemoteProviders</c> row.</summary>
    private const string ProviderCode = "dev-reference";

    private const string ContractVersion = "1.0";

    /// <summary>
    /// The one item, per tenant. Static and in-memory ON PURPOSE: persisting a demonstration item would give this
    /// temporary surface a migration, a collection and a reason to outlive its usefulness.
    /// </summary>
    private static readonly ConcurrentDictionary<string, ReferenceItemState> States = new();

    private readonly IConfiguration _configuration;

    public ReferenceWorkItemProviderController(IConfiguration configuration) => _configuration = configuration;

    /// <summary>
    /// THE ONE READ ENDPOINT A MODULE MUST OPEN. <c>GET api/v1/work-items/projection</c>.
    /// </summary>
    /// <remarks>
    /// <c>scope</c> is passed by Platform (<c>self</c> | <c>team</c>) and a module with no team concept ignores
    /// it, exactly as the in-process providers do.
    /// </remarks>
    [HttpGet("projection")]
    public IActionResult GetProjection([FromQuery] string? scope = "self")
    {
        if (!Enabled)
        {
            return NotFound();
        }

        var state = States.GetOrAdd(TenantKey, _ => new ReferenceItemState());
        return Ok(Envelope.Ok(new
        {
            contractVersion = ContractVersion,
            items = new[] { Project(state) }
        }));
    }

    /// <summary>
    /// THE ONE WRITE ENDPOINT. <c>POST api/v1/work-items/{itemId}/actions/{actionCode}</c> — the same address
    /// shape Platform itself exposes to the browser, and the same body.
    /// </summary>
    /// <remarks>
    /// <para>Every refusal carries a STABLE CODE in <c>reason_code</c>, never a sentence. Platform hands the code
    /// through unchanged and the Task Center resolves it into the reader's own language; a module that answered
    /// prose here would have that prose shown to a reader who does not speak it — the defect the error-code bridge
    /// exists to prevent. The two refusals below are real ones a module has: a stale screen offering a verb the
    /// item has moved past, and a concurrency conflict.</para>
    /// </remarks>
    [HttpPost("{itemId:guid}/actions/{actionCode}")]
    public IActionResult DispatchAction(
        Guid itemId,
        string actionCode,
        [FromBody] ReferenceActionRequest? request)
    {
        if (!Enabled)
        {
            return NotFound();
        }

        var state = States.GetOrAdd(TenantKey, _ => new ReferenceItemState());
        if (itemId != state.Id)
        {
            return NotFound(Envelope.Fail("No such work item.", 404, "REFERENCE_ITEM_NOT_FOUND"));
        }

        var expected = request?.Payload?.ExpectedVersion;
        if (expected is not null && expected != state.Version)
        {
            return Conflict(Envelope.Fail(
                "This item changed since it was read.", 409, "REFERENCE_CONCURRENCY_CONFLICT"));
        }

        lock (state)
        {
            switch (actionCode)
            {
                case "accept" when state.Status == "Pending":
                    state.Status = "InProgress";
                    break;
                case "complete" when state.Status == "InProgress":
                    state.Status = "Done";
                    break;
                default:
                    return BadRequest(Envelope.Fail(
                        $"'{actionCode}' cannot be performed while the item is '{state.Status}'.",
                        400,
                        "REFERENCE_TRANSITION_NOT_ALLOWED"));
            }

            state.Version++;
        }

        return Ok(Envelope.Ok(new { itemId = state.Id, providerCode = ProviderCode, actionCode }));
    }

    private bool Enabled => _configuration.GetValue("WorkItemReferenceProvider:Enabled", false);

    /// <summary>
    /// The tenant PLATFORM SENT, not one this service invented. Keying the state by it is what makes tenant
    /// propagation observable: two tenants see two items, and a header that failed to travel shows up immediately
    /// as one shared item titled "(no tenant header)".
    /// </summary>
    private string TenantKey
    {
        get
        {
            var header = Request.Headers["X-Tenant-Id"].ToString();
            return string.IsNullOrWhiteSpace(header) ? "(no tenant header)" : header;
        }
    }

    /// <summary>
    /// One canonical <c>WorkItemProjectionDto</c>, written by hand so the field names on the wire are visible.
    ///
    /// <para><b>Every optional field is OMITTED rather than sent as null.</b> The executable contract checks for
    /// <c>undefined</c>, and a serialized null is not undefined — an item that fails validation is DROPPED whole,
    /// taking its title and its buttons with it. <c>workItemCapabilities</c> is empty for the same discipline the
    /// onboarding note states: declare a capability only when there is data behind it, or the reader gets a card
    /// that renders empty for every item.</para>
    /// </summary>
    private object Project(ReferenceItemState state)
    {
        var terminal = state.Status == "Done";
        var actions = new List<object>();

        if (!terminal)
        {
            var code = state.Status == "Pending" ? "accept" : "complete";
            actions.Add(new
            {
                code,
                label = new { kind = "display", text = code == "accept" ? "Accept" : "Complete", locale = "und" },
                semanticType = "primary",
                enabled = true,
                source = "provider",
                requiresConfirmation = false,
                requiresReason = false,
                requiresEvidence = false,
                supportsBulk = false,
                riskLevel = "low"
            });
        }

        return new
        {
            fixtureKind = "workItem",
            id = state.Id.ToString(),
            workIntent = "task",
            assignmentMode = "direct",
            ownershipState = "assigned",
            admissionState = "admitted",
            normalizedStatus = state.Status,
            taskLifecycle = state.Status == "Pending" ? "Open" : state.Status,
            executionState = state.Status == "InProgress" ? "active" : "notStarted",
            timerState = "inactive",
            systemState = "fresh",
            actionDepth = "inline",
            title = new
            {
                kind = "display",
                // The tenant this service actually received, on the screen. Tenant propagation proven by reading.
                text = $"Reference work item — tenant {TenantKey}",
                locale = "und"
            },
            nativeStatus = new
            {
                code = state.Status,
                label = new { kind = "display", text = state.Status, locale = "und" }
            },
            source = new
            {
                providerCode = ProviderCode,
                providerContractVersion = ContractVersion,
                objectType = "referenceWorkItem",
                objectId = state.Id.ToString()
            },
            lifecycleOwner = ProviderCode,
            workItemCapabilities = Array.Empty<string>(),
            actions,
            // Required whenever an enabled inline action exists: the token the next write must echo back.
            concurrency = new { kind = "version", token = state.Version.ToString() }
        };
    }

    private sealed class ReferenceItemState
    {
        public Guid Id { get; } = Guid.NewGuid();

        public string Status { get; set; } = "Pending";

        public int Version { get; set; } = 1;
    }

    /// <summary>
    /// The shared <c>Response&lt;T&gt;</c> shape, written locally because this service's own copy carries no
    /// <c>reason_code</c> — and the reason code is the field the whole seven-language refusal bridge runs on.
    /// A module implementing this contract must emit exactly these five members.
    /// </summary>
    private sealed record Envelope(
        object? Data,
        int StatusCode,
        bool IsSuccessful,
        IReadOnlyList<string> Errors,
        [property: JsonPropertyName("reason_code")] string? ReasonCode)
    {
        public static Envelope Ok(object data) => new(data, 200, true, [], null);

        public static Envelope Fail(string error, int statusCode, string reasonCode)
            => new(null, statusCode, false, [error], reasonCode);
    }
}

/// <summary>The body Platform posts — identical to what the browser posts to Platform. One wire shape, whole chain.</summary>
public sealed record ReferenceActionRequest(string? ProviderCode, ReferenceActionPayload? Payload);

/// <summary>Only the members this reference item uses; a real module reads the ones its own commands need.</summary>
public sealed record ReferenceActionPayload(int? ExpectedVersion, string? Reason);
