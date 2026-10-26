namespace Diten.MdmService.Api.Tests;

/// <summary>
/// WP-MDM-NO-ELIGIBLE-APPROVER-01 — what each of the nine approval flows did with Platform's "nobody but you can
/// approve" start refusal. The tests assert the expected state; when NEAP_OBSERVATIONS names a file they also write
/// one line per flow (before any assertion), so the same tests measure the state before the change under a sabotage
/// run. Nothing is written otherwise.
/// </summary>
internal static class NoEligibleApproverObservations
{
    /// <summary>Platform's code (WorkflowModels.WorkflowNoEligibleApprover) on a refused start (400).</summary>
    public const string PlatformCode = "WORKFLOW_NO_ELIGIBLE_APPROVER";

    public static async Task<(int Status, string[] Errors)> ReadAsync(HttpResponseMessage response)
    {
        var body = await Envelope.ReadAsync(response);
        var errors = body.ValueKind == System.Text.Json.JsonValueKind.Object
                     && body.TryGetProperty("errors", out var list) && list.ValueKind == System.Text.Json.JsonValueKind.Array
            ? list.EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToArray()
            : [];
        return ((int)response.StatusCode, errors);
    }

    public static void Record(string flow, int status, IReadOnlyList<string> errors, object? checkpoint, object? disposition,
        string? lastFailureCode, long? nextAttemptAtUtcTicks, string aggregate)
    {
        var path = Environment.GetEnvironmentVariable("NEAP_OBSERVATIONS");
        if (string.IsNullOrWhiteSpace(path)) return;
        File.AppendAllText(path, string.Join('\t', flow, status, string.Join(",", errors), checkpoint, disposition,
            lastFailureCode ?? "-", nextAttemptAtUtcTicks.HasValue ? "retry-planned" : "no-retry", aggregate) + Environment.NewLine);
    }
}
