using Diten.Platform.Application.Features.DocumentManagementSuspension;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Application.Features.DocumentManagementControlledCopy.Services;

/// <summary>
/// MOD-0029-FU17 — adapter implementing the FU13 <see cref="IControlledCopyWithdrawalPort"/>. When a document is
/// suspended or retired, it raises a controlled-copy withdrawal plan for the entry's active copies (idempotent per open
/// plan). Best-effort: it never throws into the FU13 caller.
/// </summary>
public sealed class ControlledCopyWithdrawalPortAdapter : IControlledCopyWithdrawalPort
{
    private readonly DocumentControlledCopyService _service;

    public ControlledCopyWithdrawalPortAdapter(DocumentControlledCopyService service) => _service = service;

    public async Task OnDocumentWithdrawnAsync(DocumentMasterRegisterEntry entry, ControlledDocumentLifecycleStatus newStatus, string correlationId, CancellationToken ct)
    {
        var trigger = newStatus switch
        {
            ControlledDocumentLifecycleStatus.Suspended => CopyWithdrawalTriggerType.Suspended,
            ControlledDocumentLifecycleStatus.Retired => CopyWithdrawalTriggerType.Retired,
            ControlledDocumentLifecycleStatus.Superseded => CopyWithdrawalTriggerType.Superseded,
            _ => CopyWithdrawalTriggerType.Manual
        };

        await _service.GenerateCoreAsync(entry, trigger, dueDate: null, correlationId, ct);
    }
}
