using System.Globalization;
using Diten.Platform.Application.Features.ESignature.Services;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.ESignature.Providers;

/// <summary>
/// MOD-0022 E4 — the in-process <c>workflow / workflow-task</c> subject (pack §8, ASSUMPTION-0022-05): what an approver signs
/// when the instance's business object has no provider of its own. <c>subjectId</c> = the approval task id. The tenant-scoped
/// MOD-0023 repositories are the visibility boundary (another tenant's task is "not found").
/// <para><b>Canonical fields</b> — only what does not change while the task waits for its decision, so the subject hash taken
/// at signing still matches at approval: task / instance ids, stage + step, the step's signature meaning, the business object
/// (type, id, ref), the instance DisplayContext (title, subtitle, source, deep link, chips), the assignee and the task's
/// creation time. Status, completion and the instance's current step are deliberately NOT fields (they change on the
/// approval itself). A delegated task has another assignee → the hash changes → a signature taken before the delegation no
/// longer binds (verify <c>changed</c>).</para>
/// <para><b>E5W2 (O-E5W1-1)</b> — the summary LABELS follow the request language (Platform module: tr / en, anything else en).
/// Labels are display only: they are never fields, so the subject hash of one task is the same in every language.</para>
/// </summary>
public sealed class WorkflowTaskSignableSubjectProvider : ISignableSubjectProvider
{
    private readonly IApprovalTaskRepository _tasks;
    private readonly IWorkflowInstanceRepository _instances;
    private readonly IESignRequestLanguage _language;

    public WorkflowTaskSignableSubjectProvider(IApprovalTaskRepository tasks, IWorkflowInstanceRepository instances, IESignRequestLanguage language)
    {
        _tasks = tasks;
        _instances = instances;
        _language = language;
    }

    public string Module => WorkflowTaskSignableSubject.Module;

    public async Task<SignableSubjectResolution> ResolveAsync(string subjectType, string subjectId, CancellationToken ct)
    {
        if (!string.Equals(subjectType, WorkflowTaskSignableSubject.SubjectType, StringComparison.Ordinal) || !Guid.TryParse(subjectId, out var taskId))
        {
            return SignableSubjectResolution.NotFound;
        }

        var task = await _tasks.GetByIdAsync(taskId, ct);
        if (task is null)
        {
            return SignableSubjectResolution.NotFound;
        }

        var instance = await _instances.GetByIdAsync(task.WorkflowInstanceId, ct);
        if (instance is null || instance.TenantId != task.TenantId)
        {
            return SignableSubjectResolution.NotFound;
        }

        var display = instance.DisplayContext;
        var fields = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["taskId"] = task.Id.ToString("D"),
            ["workflowInstanceId"] = instance.Id.ToString("D"),
            ["stageCode"] = task.StageCode,
            ["stepCode"] = task.StepCode,
            ["signatureMeaning"] = task.SignatureMeaning,
            ["objectType"] = instance.ObjectType,
            ["objectId"] = instance.ObjectId,
            ["objectRef"] = instance.ObjectRef,
            ["title"] = display?.Title,
            ["subtitle"] = display?.Subtitle,
            ["sourceModule"] = display?.SourceModule,
            ["deepLinkUrl"] = display?.DeepLinkUrl,
            ["chips"] = display is { Chips.Count: > 0 } ? string.Join(" | ", display.Chips) : null,
            ["assigneeRef"] = task.AssigneeRef,
            ["taskCreatedAtUtc"] = task.CreatedAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)
        };

        var title = Cut(display?.Title ?? $"{instance.ObjectType} · {instance.ObjectRef}", 300);
        var labels = SummaryLabels.For(_language.Current());
        var summary = new List<SignableSubjectLine>
        {
            new(labels.Object, Cut($"{instance.ObjectType} · {instance.ObjectId}", 300)),
            new(labels.Step, Cut($"{task.StageCode} / {task.StepCode}", 300))
        };
        if (!string.IsNullOrWhiteSpace(display?.Subtitle)) summary.Add(new(labels.Detail, Cut(display.Subtitle, 300)));
        if (!string.IsNullOrWhiteSpace(display?.SourceModule)) summary.Add(new(labels.Source, Cut(display.SourceModule, 300)));

        return SignableSubjectResolution.Found(new SignableSubject(title, summary, null, fields));
    }

    private static string Cut(string value, int max) => value.Length <= max ? value : value[..max];

    /// <summary>The summary labels in the request language — tr, else en (Platform modules carry two languages).</summary>
    internal sealed record SummaryLabels(string Object, string Step, string Detail, string Source)
    {
        private static readonly SummaryLabels Turkish = new("Nesne", "Adım", "Ayrıntı", "Kaynak");
        private static readonly SummaryLabels English = new("Object", "Step", "Detail", "Source");

        public static SummaryLabels For(string? acceptLanguage)
        {
            var head = (acceptLanguage ?? string.Empty).Split(',', ';')[0].Trim().Split('-')[0];
            return string.Equals(head, "tr", StringComparison.OrdinalIgnoreCase) ? Turkish : English;
        }
    }
}
