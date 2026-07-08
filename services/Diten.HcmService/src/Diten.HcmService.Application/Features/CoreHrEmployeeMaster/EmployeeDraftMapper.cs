using Diten.HcmService.Domain.Entities;

namespace Diten.HcmService.Application.Features.CoreHrEmployeeMaster;

internal static class EmployeeDraftMapper
{
    public static EmployeeDraftCreateResponse ToCreateResponse(EmployeeDraftSession draftSession)
        => new(
            draftSession.Id,
            draftSession.DraftSchemaVersion,
            draftSession.CurrentStep,
            draftSession.StepStatuses,
            ToReferenceValidationResponse(draftSession.ReferenceValidationSummary),
            draftSession.Version,
            draftSession.ETag,
            draftSession.CreatedAt);

    public static EmployeeDraftResponse ToDraftResponse(EmployeeDraftSession draftSession)
        => new(
            draftSession.Id,
            draftSession.DraftSchemaVersion,
            draftSession.CurrentStep,
            draftSession.Steps.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyDictionary<string, object?>)RedactPayload(pair.Value.Payload),
                StringComparer.OrdinalIgnoreCase),
            draftSession.StepStatuses,
            ToReferenceValidationResponse(draftSession.ReferenceValidationSummary),
            draftSession.ReviewState,
            draftSession.Version,
            draftSession.ETag,
            draftSession.UpdatedAt,
            draftSession.ExpiresAt);

    public static ReferenceValidationResponse ToReferenceValidationResponse(EmployeeReferenceValidationSummary summary)
        => new(
            summary.Results.Select(ToReferenceValidationItem).ToArray(),
            summary.CanReview);

    public static ReferenceValidationItem ToReferenceValidationItem(EmployeeReferenceValidationItem item)
        => new(
            item.ReferenceType,
            item.ReferenceId,
            item.Status,
            item.IsReferenceable,
            item.Provider,
            item.ReasonCode,
            item.SafeDisplayMetadata);

    public static EmployeeReferenceValidationItem ToDomainReferenceValidationItem(ReferenceValidationItem item)
        => new()
        {
            ReferenceType = item.ReferenceType,
            ReferenceId = item.ReferenceId,
            Status = item.Status,
            IsReferenceable = item.IsReferenceable,
            Provider = item.Provider,
            ReasonCode = item.ReasonCode,
            SafeDisplayMetadata = new Dictionary<string, string>(item.SafeDisplayMetadata, StringComparer.OrdinalIgnoreCase)
        };

    private static Dictionary<string, object?> RedactPayload(Dictionary<string, object?> payload)
    {
        var redacted = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in payload)
        {
            redacted[pair.Key] = EmployeeDraftPayloadGuard.IsSensitiveKey(pair.Key)
                ? null
                : pair.Value;
        }

        return redacted;
    }
}
