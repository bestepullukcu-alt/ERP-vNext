using Diten.Platform.Application.Features.ESignature;

namespace Diten.Platform.Application.Features.Workflow;

/// <summary>
/// MOD-0022 E5W2 (CT-D19) — the one signable action MOD-0023 owns: approving a signature-required workflow step. The
/// WorkCenter approve action carries this code (<c>signatureActionCode</c>, additive) so the signing dialog asks the
/// effective policy for it and the tenant's visual mode applies. It is registered into the global e-signature action
/// catalog in-process at startup, through the same command the S2S registration endpoint sends (§4.23) — a step has no
/// action code of its own (x-workflow-addendum unchanged), so every flagged step shares this one row.
/// </summary>
public static class WorkflowSignableActions
{
    public const string Module = "workflow";
    public const string TaskApprove = "workflow.task.approve";

    private static readonly IReadOnlyDictionary<string, string> TaskApproveNames = new Dictionary<string, string>
    {
        ["en"] = "Approve workflow step",
        ["tr"] = "İş akışı adımını onayla",
        ["fr"] = "Approuver l'étape du workflow",
        ["es"] = "Aprobar el paso del flujo de trabajo",
        ["zh"] = "批准工作流步骤",
        ["ar"] = "اعتماد خطوة سير العمل",
        ["ru"] = "Утвердить шаг рабочего процесса"
    };

    /// <summary>CT-D19 defaults: required, meaning APPROVE, visual mode optional.</summary>
    public static RegisterSignableActionsCommand Registration() =>
        new(Module, [new SignableActionInput(TaskApprove, TaskApproveNames, DefaultRequired: true, DefaultMeaningCode: "APPROVE", DefaultVisualMode: "optional")]);
}
