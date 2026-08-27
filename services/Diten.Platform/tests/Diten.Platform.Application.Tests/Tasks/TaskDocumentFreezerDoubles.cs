using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Repositories;

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
/// </summary>
internal static class TaskDocumentFreezerDoubles
{
    public static TaskDocumentReferenceFreezer OverAnEmptyRegister()
        => new(new EmptyDocumentReferenceListRepository());

    private sealed class EmptyDocumentReferenceListRepository : IDocumentReferenceListRepository
    {
        public Task<DocumentReferenceListVersion> CreateVersionAsync(
            DocumentReferenceListVersion version, CancellationToken ct = default)
            => Task.FromResult(version);

        public Task<DocumentReferenceListVersion?> FindLiveVersionByHashAsync(
            string contentHash, CancellationToken ct = default)
            => Task.FromResult<DocumentReferenceListVersion?>(null);

        public Task<DocumentReferenceListVersion?> GetVersionAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<DocumentReferenceListVersion?>(null);

        public Task UpdateVersionAsync(DocumentReferenceListVersion version, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task AddEntriesAsync(IReadOnlyList<DocumentReferenceEntry> entries, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<DocumentReferenceListVersion>> ListVersionsAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<DocumentReferenceListVersion>>(Array.Empty<DocumentReferenceListVersion>());

        public Task<DocumentReferenceListVersion?> GetLatestVersionAsync(CancellationToken ct = default)
            => Task.FromResult<DocumentReferenceListVersion?>(null);

        public Task<IReadOnlyList<DocumentReferenceEntry>> SearchAsync(
            Guid listVersionId, string? term, int limit, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<DocumentReferenceEntry>>(Array.Empty<DocumentReferenceEntry>());

        public Task<IReadOnlyList<DocumentReferenceEntry>> GetEntriesByUidsAsync(
            Guid listVersionId, IReadOnlyCollection<string> documentUids, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<DocumentReferenceEntry>>(Array.Empty<DocumentReferenceEntry>());
    }
}
