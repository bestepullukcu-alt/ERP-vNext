using System.Text.Json;
using Diten.Platform.Domain.Entities.Workflow;

namespace Diten.Platform.Application.Features.Workflow.Handlers.CommandHandlers;

internal sealed record WorkflowRuntimeStep(
    string StageCode,
    string StepCode,
    string StepName,
    string StepType,
    IReadOnlyList<string> CandidatePrincipalIds,
    bool CommentRequired,
    bool EvidenceRequired,
    int? DueInMinutes,
    int? EscalateAfterMinutes = null,
    int? TimeoutAfterMinutes = null,
    IReadOnlyList<string>? EscalationPrincipalIds = null);

internal static class WorkflowDefinitionRuntimePlan
{
    private const string DefaultStageCode = "stage-1";
    private const string DefaultStepCode = "step-1";

    public static IReadOnlyList<WorkflowRuntimeStep> FromVersion(WorkflowTemplateVersion? version)
    {
        if (version is null || string.IsNullOrWhiteSpace(version.DefinitionJson))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(version.DefinitionJson);
            if (!document.RootElement.TryGetProperty("stages", out var stages) ||
                stages.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var steps = new List<WorkflowRuntimeStep>();
            foreach (var stage in stages.EnumerateArray())
            {
                var stageCode = ReadString(stage, "code") ?? DefaultStageCode;
                if (!stage.TryGetProperty("steps", out var stageSteps) ||
                    stageSteps.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var step in stageSteps.EnumerateArray())
                {
                    var stepCode = ReadString(step, "code") ?? DefaultStepCode;
                    var stepName = ReadString(step, "name") ?? stepCode;
                    var stepType = ReadString(step, "type") ?? "approval";
                    var candidates = ReadCandidates(step);
                    var commentRequired = ReadBool(step, "requirements", "commentRequired");
                    var evidenceRequired = ReadBool(step, "requirements", "evidenceRequired");
                    var dueInMinutes = ReadInt(step, "sla", "dueInMinutes");
                    var escalateAfterMinutes = ReadInt(step, "sla", "escalateAfterMinutes");
                    var timeoutAfterMinutes = ReadInt(step, "sla", "timeoutAfterMinutes");
                    var escalationPrincipalIds = ReadEscalationPrincipals(step);

                    steps.Add(new WorkflowRuntimeStep(
                        stageCode,
                        stepCode,
                        stepName,
                        stepType,
                        candidates,
                        commentRequired,
                        evidenceRequired,
                        dueInMinutes,
                        escalateAfterMinutes,
                        timeoutAfterMinutes,
                        escalationPrincipalIds));
                }
            }

            return steps;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static WorkflowRuntimeStep FallbackStep(IReadOnlyList<string> candidates, bool commentRequired, bool evidenceRequired) =>
        new(
            DefaultStageCode,
            DefaultStepCode,
            "Approval",
            "approval",
            candidates,
            commentRequired,
            evidenceRequired,
            null);

    public static int IndexOf(IReadOnlyList<WorkflowRuntimeStep> steps, string stageCode, string stepCode)
    {
        for (var i = 0; i < steps.Count; i++)
        {
            if (string.Equals(steps[i].StageCode, stageCode, StringComparison.Ordinal) &&
                string.Equals(steps[i].StepCode, stepCode, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private static IReadOnlyList<string> ReadCandidates(JsonElement step)
    {
        if (!step.TryGetProperty("assignment", out var assignment) ||
            !assignment.TryGetProperty("candidatePrincipalIds", out var candidates) ||
            candidates.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return candidates.EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String)
            .Select(x => x.GetString())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static IReadOnlyList<string> ReadEscalationPrincipals(JsonElement step)
    {
        if (!step.TryGetProperty("sla", out var sla) ||
            !sla.TryGetProperty("escalationPrincipalIds", out var principals) ||
            principals.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return principals.EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String)
            .Select(x => x.GetString())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static string? ReadString(JsonElement source, string property)
    {
        if (!source.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = value.GetString();
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    private static bool ReadBool(JsonElement source, string objectProperty, string property)
    {
        if (!source.TryGetProperty(objectProperty, out var obj) ||
            !obj.TryGetProperty(property, out var value))
        {
            return false;
        }

        return value.ValueKind == JsonValueKind.True || (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var parsed) && parsed);
    }

    private static int? ReadInt(JsonElement source, string objectProperty, string property)
    {
        if (!source.TryGetProperty(objectProperty, out var obj) ||
            !obj.TryGetProperty(property, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) && number > 0)
        {
            return number;
        }

        return value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed) && parsed > 0
            ? parsed
            : null;
    }
}
