namespace Diten.Platform.Application.Features.RecordCodes;

/// <summary>RECORD-CODE-ALLOCATE 1.0.0 §4 — the <c>reason_code</c> values of the allocate endpoint.</summary>
public static class RecordCodeReasonCodes
{
    public const string RequestTooLarge = "RECORD_CODE_REQUEST_TOO_LARGE";
    public const string Unauthenticated = "RECORD_CODE_UNAUTHENTICATED";
    public const string Forbidden = "RECORD_CODE_FORBIDDEN";
}
