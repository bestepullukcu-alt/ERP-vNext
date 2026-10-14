using Diten.CrmService.Application.Features.VisitQr;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;

namespace Diten.CrmService.Application.Tests.VisitExecution;

/// <summary>WP-VW-W3E (W3E-BE-a) — the QR codes in memory WITH the database's rule: one ACTIVE row per (tenant, account)
/// (a second active insert answers false, as the partial unique index does), a revoke only while active at the expected
/// version. Thread-safe so the concurrency test can race two issues.</summary>
internal sealed class FakeInstitutionQrCodeRepository : IInstitutionQrCodeRepository
{
    private readonly object _gate = new();
    public List<InstitutionQrCode> Items { get; } = new();
    public bool ThrowOnScan { get; set; }
    public int ScansSince { get; set; }

    /// <summary>A pause between the existence check and the write of an insert (the race window of two issues).</summary>
    public Func<Task>? BeforeInsert { get; set; }

    public Task<InstitutionQrCode?> GetActiveAsync(Guid tenantId, Guid accountId, CancellationToken ct)
    {
        lock (_gate) return Task.FromResult(Items.FirstOrDefault(c => c.TenantId == tenantId && c.AccountId == accountId && c.IsActive()));
    }

    public Task<IReadOnlyList<InstitutionQrCode>> ListActiveByAccountsAsync(Guid tenantId, IReadOnlyCollection<Guid> accountIds, CancellationToken ct)
    {
        lock (_gate) return Task.FromResult<IReadOnlyList<InstitutionQrCode>>(Items.Where(c => c.TenantId == tenantId && accountIds.Contains(c.AccountId) && c.IsActive()).ToList());
    }

    public Task<IReadOnlyList<InstitutionQrCode>> ListByAccountAsync(Guid tenantId, Guid accountId, CancellationToken ct)
    {
        lock (_gate) return Task.FromResult<IReadOnlyList<InstitutionQrCode>>(Items.Where(c => c.TenantId == tenantId && c.AccountId == accountId).OrderByDescending(c => c.CodeVersion).ToList());
    }

    public Task<IReadOnlyList<Guid>> ListAccountIdsByStatusAsync(Guid tenantId, string status, CancellationToken ct)
    {
        lock (_gate) return Task.FromResult<IReadOnlyList<Guid>>(Items.Where(c => c.TenantId == tenantId && c.Status == status).Select(c => c.AccountId).Distinct().ToList());
    }

    public Task<IReadOnlyList<InstitutionQrCode>> ListLatestByAccountsAsync(Guid tenantId, IReadOnlyCollection<Guid> accountIds, CancellationToken ct)
    {
        lock (_gate)
            return Task.FromResult<IReadOnlyList<InstitutionQrCode>>(Items.Where(c => c.TenantId == tenantId && accountIds.Contains(c.AccountId))
                .GroupBy(c => c.AccountId).Select(g => g.FirstOrDefault(c => c.IsActive()) ?? g.OrderByDescending(c => c.CodeVersion).First()).ToList());
    }

    public async Task<bool> InsertAsync(InstitutionQrCode code, CancellationToken ct)
    {
        if (BeforeInsert is { } pause)
        {
            await pause();
        }

        lock (_gate)
        {
            if (code.IsActive() && Items.Any(c => c.TenantId == code.TenantId && c.AccountId == code.AccountId && c.IsActive()))
            {
                return false;
            }

            Items.Add(code);
            return true;
        }
    }

    public Task<bool> RevokeAsync(Guid tenantId, Guid id, int expectedCodeVersion, string? by, DateTimeOffset at, string reason, CancellationToken ct)
    {
        lock (_gate)
        {
            var code = Items.FirstOrDefault(c => c.TenantId == tenantId && c.Id == id && c.IsActive() && c.CodeVersion == expectedCodeVersion);
            if (code is null) return Task.FromResult(false);
            code.Status = InstitutionQrCodeStatuses.Revoked;
            code.RevokedAt = at;
            code.RevokedBy = by;
            code.RevokeReason = reason;
            return Task.FromResult(true);
        }
    }

    public Task RecordScanAsync(Guid tenantId, Guid id, DateTimeOffset at, CancellationToken ct)
    {
        if (ThrowOnScan) throw new InvalidOperationException("database down");
        lock (_gate)
        {
            var code = Items.First(c => c.TenantId == tenantId && c.Id == id);
            code.ScanCount++;
            code.LastScannedAt = at;
        }

        return Task.CompletedTask;
    }

    public Task<int> CountScansSinceAsync(Guid tenantId, Guid accountId, DateTimeOffset since, CancellationToken ct) => Task.FromResult(ScansSince);
}

internal sealed class FakeInstitutionQrEventRepository : IInstitutionQrEventRepository
{
    public List<InstitutionQrEvent> Items { get; } = new();

    public Task InsertManyAsync(IReadOnlyCollection<InstitutionQrEvent> events, CancellationToken ct)
    {
        lock (Items) Items.AddRange(events);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<InstitutionQrEvent>> ListByAccountAsync(Guid tenantId, Guid accountId, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<InstitutionQrEvent>>(Items.Where(e => e.TenantId == tenantId && e.AccountId == accountId).OrderByDescending(e => e.At).ToList());
}

/// <summary>The test's OWN keys (never a configured secret): two 32-byte keys, k1 active unless told otherwise.</summary>
internal sealed class TestQrKeys(VisitQrKeys keys) : IVisitQrKeyRing
{
    public static readonly byte[] K1 = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray();
    public static readonly byte[] K2 = Enumerable.Range(101, 32).Select(i => (byte)i).ToArray();

    public static TestQrKeys Active(string activeKeyId = "k1", bool withK2 = false)
    {
        var map = new Dictionary<string, byte[]>(StringComparer.Ordinal) { ["k1"] = K1 };
        if (withK2) map["k2"] = K2;
        return new TestQrKeys(new VisitQrKeys(activeKeyId, map));
    }

    public static TestQrKeys None() => new(VisitQrKeys.None);

    public VisitQrKeys Keys { get; set; } = keys;

    public Task<VisitQrKeys> GetAsync(CancellationToken ct) => Task.FromResult(Keys);
}
