using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Models;
using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Services;
using Diten.Platform.Application.Features.Tasks.Services;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// The freezer for tests that are not about citations.
///
/// ⚠ WHY A DOUBLE AND NOT A NULL. The freezer used to be an OPTIONAL constructor argument on both task write
/// handlers, and fifteen test call sites simply omitted it. That is exactly what made the production defect
/// invisible: omitting it was normal, so nobody noticed that DI omitted it too, and every document citation
/// an author entered was silently discarded (measured live 2026-08-26). The argument is required now, so
/// these call sites have to say what they want — and what they want is "a real freezer over an empty
/// register", which resolves nothing and refuses nothing.
///
/// ⚠ IT IS A REAL FREEZER, NOT A STUB. A stub returning "no change" would let a handler regress to writing
/// citations without freezing them and no test would see it. Over an empty register, the real freezer keeps
/// its real behaviour: a payload with no UIDs passes through untouched, and a payload that DOES cite
/// something is refused — which is the correct answer when the register holds nothing.
///
/// <para>DCP-005 Step 2 — the double moved from an empty CSV repository to an empty
/// <see cref="IControlledDocumentCitationPort"/>: <c>ResolveAsync</c> always returns zero items, so every UID a
/// test asks to freeze here is genuinely Unresolved (the register "holds nothing" is now expressed as "resolves
/// nothing", not "no list was ever imported") and the refusal reason code changed with it — see
/// <c>TaskReasonCodes.DocumentReferenceNotFound</c> below.</para>
/// </summary>
internal static class TaskDocumentFreezerDoubles
{
    public static TaskDocumentReferenceFreezer OverAnEmptyRegister()
        => new(new EmptyControlledDocumentCitationPort());

    private sealed class EmptyControlledDocumentCitationPort : IControlledDocumentCitationPort
    {
        public Task<DocumentCitationResult> ResolveAsync(DocumentCitationQuery query, CancellationToken ct)
            => Task.FromResult(new DocumentCitationResult([]));

        public Task<DocumentCitationResult> SearchAsync(string? term, int limit, CancellationToken ct)
            => Task.FromResult(new DocumentCitationResult([]));
    }
}
