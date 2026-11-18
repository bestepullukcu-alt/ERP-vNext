using Diten.MdmService.Application.Contracts.RecordCodes;

namespace Diten.MdmService.Api.Tests;

/// <summary>The test host's own code service (RCS-001 lives in Platform): a counter, every call recorded, the answer replaceable.</summary>
public sealed class FakeRecordCodes : IRecordCodeAllocator
{
    private long _sequence;

    public sealed record Call(RecordCodePrefixScope Scope, Guid? LegalEntityId, string RecordType, string IdempotencyKey);

    public List<Call> Calls { get; } = [];

    /// <summary>When set, answers instead of allocating; a test restores it (null) when done.</summary>
    public Func<RecordCodeAllocationResult>? Answer { get; set; }

    public Task<RecordCodeAllocationResult> AllocateAsync(
        RecordCodePrefixScope prefixScope, Guid? legalEntityId, string recordType, string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        lock (Calls) Calls.Add(new Call(prefixScope, legalEntityId, recordType, idempotencyKey));
        if (Answer is { } answer) return Task.FromResult(answer());
        var number = Interlocked.Increment(ref _sequence);
        return Task.FromResult(RecordCodeAllocationResult.Allocated($"GMG-BNK-{number:D6}", number));
    }
}
