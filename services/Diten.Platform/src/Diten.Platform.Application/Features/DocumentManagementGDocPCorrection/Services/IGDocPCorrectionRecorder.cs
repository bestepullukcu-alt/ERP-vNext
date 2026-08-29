using Diten.Platform.Application.Common;

namespace Diten.Platform.Application.Features.DocumentManagementGDocPCorrection.Services;

/// <summary>
/// MOD-0029-FU21 — the EXTENSION POINT by which an existing update command can leave a GDocP correction trail
/// without FU21 reaching into that command (GMG-QMS-SOP-0001 §21).
///
/// Deliberately NOT wired into any existing FU06–FU20 update command in this FU. Doing so would change those
/// commands' validation surface (a correction reason would become mandatory on paths that do not ask for one
/// today) and would break their existing tests. The intended adoption path is per-feature and incremental: inject
/// this recorder, and call it from the update handler once that feature's contract is extended to carry a reason.
///
/// Implemented by <see cref="DocumentGDocPCorrectionService"/>, so a caller gets the full evaluator — policy
/// resolution, risk classification, backdating detection and review routing — rather than a bare insert.
/// </summary>
public interface IGDocPCorrectionRecorder
{
    /// <summary>
    /// Records one field correction. Returns the created record, or a failure response carrying the reason code
    /// when the correction does not satisfy the resolved GDocP requirements.
    /// </summary>
    Task<Response<GDocPCorrectionRecordModel>> RecordCorrectionAsync(
        RecordGDocPCorrectionInput input, string correlationId, CancellationToken ct);
}
