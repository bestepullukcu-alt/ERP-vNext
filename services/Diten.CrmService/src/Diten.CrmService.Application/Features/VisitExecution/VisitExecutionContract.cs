namespace Diten.CrmService.Application.Features.VisitExecution;

/// <summary>
/// WP-VW-W3 (BE-a) permission keys (PKS-001). <b>DEFINITION ONLY</b> — no seed, no grant; the Platform
/// <c>CrmManifestProvider</c> publishes them and a role receives them from the role-permission screen. Another rep's
/// visit is read with the existing <c>crm.planned-visit.read-all</c>.
/// </summary>
public static class VisitExecutionPermissions
{
    public const string Read = "crm.visit-execution.read";
    public const string Record = "crm.visit-execution.record";
    public const string PolicyManage = "crm.visit-evidence-policy.manage";

    /// <summary>WP-VW-W3 (W3-BE-c) — read the evidence settings (policy, history, effective rules, reps) without managing.</summary>
    public const string PolicyRead = "crm.visit-evidence-policy.read";

    public static readonly IReadOnlyList<string> All = new[] { Read, Record, PolicyManage, PolicyRead };
}

/// <summary>WP-VW-W3 (BE-a) — the machine codes of the visit execution endpoints (§2.2, fixed: Web W3 and mobile read
/// them). The status code is on each line.</summary>
public static class VisitExecutionErrorCodes
{
    public const string NotFound = "visit_execution_not_found";                         // 404

    public const string NotToday = "visit_not_today";                                   // 409
    public const string NotConfirmed = "visit_not_confirmed";                           // 409
    public const string AlreadyStarted = "visit_already_started";                       // 409
    public const string AlreadyReported = "visit_already_reported";                     // 409
    public const string LocationRequired = "visit_location_required";                   // 409
    public const string LocationTooFar = "visit_location_too_far";                      // 409
    public const string LocationInaccurate = "visit_location_inaccurate";               // 409
    public const string AccountLocationUnknown = "visit_account_location_unknown";      // 409
    public const string ExecutionFinished = "visit_execution_finished";                 // 409
    public const string ExecutionNotFinished = "visit_execution_not_finished";          // 409
    public const string SignatureExists = "visit_signature_exists";                     // 409
    public const string ReportDeadlinePassed = "visit_report_deadline_passed";          // 409
    public const string ExecutionStarted = "visit_execution_started";                   // 409 (W1 / W2 commands)
    public const string ConcurrencyConflict = "visit_execution_concurrency_conflict";   // 409

    public const string PhotoRequired = "visit_photo_required";                         // 400
    public const string PhotoInvalid = "visit_photo_invalid";                           // 400
    public const string CaptureInvalid = "visit_capture_invalid";                       // 400
    public const string CoVisitorInvalid = "visit_co_visitor_invalid";                  // 400
    public const string PolicyInvalid = "visit_evidence_policy_invalid";                // 400

    public const string EvidenceStoreUnavailable = "visit_evidence_store_unavailable";  // 503

    /// <summary>The prefix of the extra error line a too-far start carries: <c>distance_meters=1234</c>.</summary>
    public const string DistancePrefix = "distance_meters=";

    // WP-VW-W3E (W3E-BE-a, §2.3 / §2.4) — the institution QR code.
    public const string QrRequired = "visit_qr_required";                                // 400
    public const string QrInvalid = "visit_qr_invalid";                                  // 400
    public const string QrWrongAccount = "visit_qr_wrong_account";                       // 409
    public const string QrRevoked = "visit_qr_revoked";                                  // 409
    public const string QrSigningUnavailable = "visit_qr_signing_unavailable";           // 503
    public const string QrAccountNotFound = "visit_qr_account_not_found";                // 404
    public const string QrRequestInvalid = "visit_qr_request_invalid";                   // 400
    public const string QrNotActive = "visit_qr_not_active";                             // 409

    /// <summary>The prefix of the extra error line a wrong-institution QR start carries: <c>scanned_account_id=…</c>
    /// (same tenant only — a foreign tenant's code is <c>invalid</c>, never named).</summary>
    public const string ScannedAccountPrefix = "scanned_account_id=";

    public static readonly IReadOnlyList<string> All = new[]
    {
        NotFound, NotToday, NotConfirmed, AlreadyStarted, AlreadyReported, LocationRequired, LocationTooFar,
        LocationInaccurate, AccountLocationUnknown, ExecutionFinished, ExecutionNotFinished, SignatureExists,
        ReportDeadlinePassed, ExecutionStarted, ConcurrencyConflict, PhotoRequired, PhotoInvalid, CaptureInvalid,
        CoVisitorInvalid, PolicyInvalid, EvidenceStoreUnavailable,
        QrRequired, QrInvalid, QrWrongAccount, QrRevoked, QrSigningUnavailable, QrAccountNotFound, QrRequestInvalid, QrNotActive
    };
}
