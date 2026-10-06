namespace Diten.CrmService.Api.Models.CRM;

// SCMM-15 (CAND-CAP-0011) ContentSetRevision request models. TenantId is NEVER in a request body (server-resolved).
// The revision id comes from the route on the decision endpoint.

/// <summary>Submit a content-set draft for review. <see cref="ExpectedVersion"/> (optional) guards a stale submit.</summary>
public sealed record SubmitContentSetRevisionRequest(Guid ContentSetId, int? ExpectedVersion = null);

/// <summary>Record a review decision. <see cref="Decision"/> is "approve" | "reject".</summary>
public sealed record RecordReviewDecisionRequest(string Decision, string? Reason = null);

/// <summary>SCMM-17 — managed withdrawal of a released revision. <see cref="Reason"/> is required.</summary>
public sealed record WithdrawContentSetRevisionRequest(string Reason);
