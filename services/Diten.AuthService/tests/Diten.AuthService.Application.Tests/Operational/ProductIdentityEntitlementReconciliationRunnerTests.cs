using System.Security.Claims;
using Diten.AuthService.Api.Operational;
using Diten.AuthService.Application.Common.Entitlements;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Tests.Operational;

public sealed class ProductIdentityEntitlementReconciliationRunnerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Replay_WhenProcessObservationChangesOrFails_DemotesWithoutCommit(bool unavailable)
    {
        var plan = ReconciliationTestData.Plan(); var store = new RecordingReconciliationStore(plan); var source = new RecordingEntitlementSource(plan);
        var runner = new EntitlementReconciliationOperationalRunner(store, new RecordingTokenService(plan.ActorId), source,
            _ => unavailable ? throw new InvalidOperationException("TEST_PROCESS_PROBE_FAILED") : Task.FromResult("changed-process-set"));
        var method = typeof(EntitlementReconciliationOperationalRunner).GetMethod("ReplayObservedAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var result = await (Task<string>)method.Invoke(runner, [plan, store.Receipt with { State = "SUCCESS" }, "test-token",
            DateTimeOffset.UtcNow.AddMinutes(2), "original-process-set", CancellationToken.None])!;
        Assert.Equal("COMMITTED_MANUAL_RECONCILIATION_REQUIRED LOCAL_CONCURRENCY_DRIFT_AFTER_COMMIT", result);
        Assert.Equal(0, store.Commits); Assert.False(store.LastSuccess);
    }
    [Theory]
    [InlineData("quiescence", "LOCAL_CONCURRENCY_DRIFT_AFTER_COMMIT")]
    [InlineData("local", "LOCAL_CONCURRENCY_DRIFT_AFTER_COMMIT")]
    [InlineData("version", "LOCAL_CONCURRENCY_DRIFT_AFTER_COMMIT")]
    [InlineData("operator", "OPERATOR_AUTHORITY_DRIFT_AFTER_COMMIT")]
    [InlineData("platform-operator", "OPERATOR_AUTHORITY_DRIFT_AFTER_COMMIT")]
    [InlineData("tenant", "EXTERNAL_AUTHORITY_DRIFT_AFTER_COMMIT")]
    [InlineData("module", "EXTERNAL_AUTHORITY_DRIFT_AFTER_COMMIT")]
    [InlineData("authority", "EXTERNAL_AUTHORITY_DRIFT_AFTER_COMMIT")]
    [InlineData("unavailable", "POST_COMMIT_AUTHORITY_UNAVAILABLE")]
    public async Task Replay_WhenSuccessHasCurrentDrift_DemotesWithExactReasonAndNeverCommits(string drift, string reason)
    {
        var plan = ReconciliationTestData.Plan(); var store = new RecordingReconciliationStore(plan);
        var source = new RecordingEntitlementSource(plan); var receipt = store.Receipt with { State = "SUCCESS" };
        switch (drift)
        {
            case "quiescence": store.Snapshot = store.Snapshot with { QuiescenceFingerprint = "different" }; break;
            case "local": store.Snapshot = store.Snapshot with { Fingerprint = "different" }; break;
            case "version": store.Snapshot = store.Snapshot with { RoleAssignmentVersion = 9 }; break;
            case "operator": store.Snapshot = store.Snapshot with { Operator = store.Snapshot.Operator with { IsAuthorized = false } }; break;
            case "platform-operator": source.Snapshot = source.Snapshot with { OperatorActive = false }; break;
            case "tenant": source.Snapshot = source.Snapshot with { TenantActive = false }; break;
            case "module": source.Snapshot = new(plan.TenantId, plan.ModuleCode, "modules", "operator", "tenant", "operator@example.test",
                true, true, [], plan.AuthorityFingerprint); break;
            case "authority": source.Snapshot = source.Snapshot with { Fingerprint = "different" }; break;
            case "unavailable": source.Unavailable = true; break;
        }
        var result = await ReplayAsync(plan, receipt, store, source);
        Assert.Equal("COMMITTED_MANUAL_RECONCILIATION_REQUIRED " + reason, result);
        Assert.Equal(reason, store.LastReason); Assert.False(store.LastSuccess);
        Assert.Equal(1, store.Reads); Assert.Equal(1, source.Reads); Assert.Equal(0, store.Commits);
    }

    [Theory]
    [InlineData(true, "OPERATOR_AUTHORITY_DRIFT_AFTER_COMMIT")]
    [InlineData(false, "EXTERNAL_AUTHORITY_DRIFT_AFTER_COMMIT")]
    public async Task Replay_WhenSeveralDriftsCoexist_PreservesOperatorThenExternalPriority(bool operatorInactive, string reason)
    {
        var plan = ReconciliationTestData.Plan(); var store = new RecordingReconciliationStore(plan);
        store.Snapshot = store.Snapshot with { Fingerprint = "local-drift" };
        var source = new RecordingEntitlementSource(plan);
        source.Snapshot = source.Snapshot with { OperatorActive = !operatorInactive, TenantActive = false };
        Assert.EndsWith(reason, await ReplayAsync(plan, store.Receipt with { State = "SUCCESS" }, store, source));
        Assert.Equal(reason, store.LastReason);
    }

    [Fact]
    public async Task Replay_WhenManualHoldAndCurrentAuthorityUnavailable_ObservesButNeverOverwritesReason()
    {
        var plan = ReconciliationTestData.Plan(); var store = new RecordingReconciliationStore(plan);
        var source = new RecordingEntitlementSource(plan) { Unavailable = true };
        var receipt = store.Receipt with { State = "COMMITTED_MANUAL_RECONCILIATION_REQUIRED", ManualHold = true,
            Reason = "OPERATOR_AUTHORITY_DRIFT_AFTER_COMMIT" };
        var result = await ReplayAsync(plan, receipt, store, source);
        Assert.Equal("COMMITTED_MANUAL_RECONCILIATION_REQUIRED OPERATOR_AUTHORITY_DRIFT_AFTER_COMMIT CURRENT_OBSERVATION=POST_COMMIT_AUTHORITY_UNAVAILABLE", result);
        Assert.Equal(1, store.Reads); Assert.Equal(1, source.Reads); Assert.Equal(0, store.Finalizations);
    }

    [Fact]
    public async Task Replay_WhenSuccessStillMatches_ReadsAllAuthorityWithoutWriting()
    {
        var plan = ReconciliationTestData.Plan(); var store = new RecordingReconciliationStore(plan); var source = new RecordingEntitlementSource(plan);
        Assert.Equal("SUCCESS REPLAY", await ReplayAsync(plan, store.Receipt with { State = "SUCCESS" }, store, source));
        Assert.Equal(1, store.Reads); Assert.Equal(1, source.Reads); Assert.Equal(0, store.Finalizations); Assert.Equal(0, store.Commits);
    }
    [Fact]
    public async Task Replay_WhenManualOutcomeExistsAndPostStateMatches_RechecksAuthorityButRemainsManual()
    {
        var plan = ReconciliationTestData.Plan();
        var store = new RecordingReconciliationStore(plan);
        var source = new RecordingEntitlementSource(plan);
        var receipt = store.Receipt with { State = "COMMITTED_MANUAL_RECONCILIATION_REQUIRED", Reason = "LOCAL_CONCURRENCY_DRIFT_AFTER_COMMIT" };
        var result = await ReplayAsync(plan, receipt, store, source);
        Assert.StartsWith("COMMITTED_MANUAL_RECONCILIATION_REQUIRED", result);
        Assert.Equal(1, store.Reads);
        Assert.Equal(1, source.Reads);
        Assert.Equal(0, store.Finalizations);
        Assert.Equal(0, store.Commits);
    }

    [Fact]
    public async Task Replay_WhenOnlyLocalCommitPendingReceiptExists_RechecksAuthorityButNeverPromotesToSuccess()
    {
        var plan = ReconciliationTestData.Plan();
        var store = new RecordingReconciliationStore(plan);
        var source = new RecordingEntitlementSource(plan);
        var result = await ReplayAsync(plan, store.Receipt, store, source);
        Assert.Equal("COMMITTED_MANUAL_RECONCILIATION_REQUIRED POST_COMMIT_FINALIZATION_MISSING", result);
        Assert.Equal(1, store.Reads);
        Assert.Equal(1, source.Reads);
        Assert.Equal(1, store.Finalizations);
        Assert.False(store.LastSuccess);
        Assert.Equal("POST_COMMIT_FINALIZATION_MISSING", store.LastReason);
        Assert.Equal(0, store.Commits);
    }

    internal static Task<string> ReplayAsync(EntitlementReconciliationPlan plan, EntitlementOperationReceipt receipt,
        RecordingReconciliationStore store, RecordingEntitlementSource source)
    {
        var runner = new EntitlementReconciliationOperationalRunner(store, new RecordingTokenService(plan.ActorId), source,
            _ => Task.FromResult("process-observation"));
        var method = typeof(EntitlementReconciliationOperationalRunner).GetMethod("ReplayObservedAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (Task<string>)method.Invoke(runner, [plan, receipt, "test-token-not-a-credential", DateTimeOffset.UtcNow.AddMinutes(2),
            "process-observation", CancellationToken.None])!;
    }
}

internal static class ReconciliationTestData
{
    internal const string Digest = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    internal static EntitlementReconciliationPlan Plan()
    {
        var operation = Guid.NewGuid();
        var role = Guid.NewGuid();
        var rows = Enumerable.Range(0, 6).Select(i => new EntitlementReconciliationRow("add", Guid.NewGuid(), role,
            "ProductDataSteward", Guid.NewGuid(), "mdm.test." + i, EntitlementReconciliationPlan.Module,
            PermissionScope.Tenant, GrantSource.Module, EntitlementReconciliationPlan.Module)).ToList();
        rows.Add(new("remove", EntitlementReconciliationPlan.RemovedGrant, role, "ProductIdentityRetirementSteward",
            Guid.NewGuid(), "mdm.lskus.retire", EntitlementReconciliationPlan.Module, PermissionScope.Tenant,
            GrantSource.Module, EntitlementReconciliationPlan.Module));
        return new(1, operation, EntitlementReconciliationPlan.TargetTenant, EntitlementReconciliationPlan.Module,
            Guid.NewGuid(), "PlatformAdministrator", Digest, Digest, Digest, Digest, 7, [], [], rows,
            Digest, Digest, new string('a', 40), DateTimeOffset.UtcNow);
    }
}

internal sealed class RecordingReconciliationStore(EntitlementReconciliationPlan plan) : IEntitlementReconciliationOperationStore
{
    public int Reads { get; private set; }
    public int Commits { get; private set; }
    public int Finalizations { get; private set; }
    public bool LastSuccess { get; private set; }
    public string? LastReason { get; private set; }
    public bool ReturnNoReceipt { get; set; }
    public bool AllowCommit { get; set; }
    public EntitlementReconciliationPlan? CommittedPlan { get; private set; }
    public EntitlementLocalSnapshot Snapshot { get; set; } = new(plan.TenantId, 8, [], [], [], [], [],
        new(plan.ActorId, EntitlementReconciliationPlan.AdminTenant, "operator@example.test", true, plan.OperatorFingerprint),
        plan.LocalFingerprint, plan.QuiescenceFingerprint);
    public EntitlementOperationReceipt Receipt { get; set; } = new(plan.OperationId, plan.Sha256(),
        "LOCAL_COMMITTED_AUTHORITY_REVALIDATION_PENDING", null, plan.LocalFingerprint, 8, false);
    public Task VerifyStorageAsync(CancellationToken ct) => Task.CompletedTask;
    public Task<EntitlementLocalSnapshot> ReadAsync(Guid tenantId, Guid operatorId, CancellationToken ct)
    { Reads++; return Task.FromResult(Snapshot); }
    public Task<EntitlementOperationReceipt?> ReadReceiptAsync(Guid operationId, CancellationToken ct) => Task.FromResult<EntitlementOperationReceipt?>(ReturnNoReceipt ? null : Receipt);
    public Task<EntitlementLocalCommitResult> CommitAsync(EntitlementReconciliationPlan value, CancellationToken ct)
    {
        Commits++; if (!AllowCommit) throw new InvalidOperationException("REPLAY_MUST_NOT_COMMIT");
        CommittedPlan = value; Snapshot = Snapshot with { RoleAssignmentVersion = value.ExpectedRoleAssignmentVersion + 1 };
        return Task.FromResult(new EntitlementLocalCommitResult(Receipt.State, Receipt));
    }
    public Task<EntitlementOperationReceipt> FinalizeAsync(EntitlementReconciliationPlan value, bool success,
        string? reason, string postStateFingerprint, CancellationToken ct)
    {
        Finalizations++; LastSuccess = success; LastReason = reason;
        return Task.FromResult(Receipt with { State = success ? "SUCCESS" : "COMMITTED_MANUAL_RECONCILIATION_REQUIRED", Reason = reason });
    }
}

internal sealed class RecordingEntitlementSource(EntitlementReconciliationPlan plan) : ITenantEntitlementClient
{
    public bool Unavailable { get; set; }
    public int Reads { get; private set; }
    public EntitlementAuthoritySnapshot Snapshot { get; set; } = new(plan.TenantId, plan.ModuleCode,
        "https://authority.test/modules", "https://authority.test/operator", "https://authority.test/tenant",
        "operator@example.test", true, true, ["expected-key"], plan.AuthorityFingerprint);
    public Task<EntitlementAuthoritySnapshot> ReadReconciliationAuthorityAsync(Guid tenantId, string email, CancellationToken ct)
    { Reads++; if (Unavailable) throw new HttpRequestException("test authority unavailable"); return Task.FromResult(Snapshot); }
    public Task<IReadOnlyList<string>> GetEntitledModuleCodesAsync(Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
    public Task<IReadOnlyList<EntitledModulePermissionKeys>> GetEntitledModulesWithPermissionKeysAsync(Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
    public Task<TenantEntitlementReadResult> ReadEntitledModulesWithPermissionKeysAsync(Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
}

internal sealed class RecordingTokenService(Guid actor) : ITokenService
{
    public ClaimsPrincipal GetPrincipalFromCurrentToken(string token, DateTimeOffset deadline) => new(new ClaimsIdentity(
        [new("sub", actor.ToString("D")), new("tenant_id", EntitlementReconciliationPlan.AdminTenant.ToString("D")),
            new("actor_type", "platform_admin"), new("pwd_change_required", "false"),
            new("permission", EntitlementReconciliationPlan.RequiredPermission)], "test"));
    public string GenerateAccessToken(User u, IEnumerable<string> r, IEnumerable<string> p) => throw new NotSupportedException();
    public string GenerateAccessToken(User u, IEnumerable<string> r, IEnumerable<string> p, int e) => throw new NotSupportedException();
    public string GeneratePlatformAccessToken(Guid u, string e, string? f, string? l, Guid t, string a, IEnumerable<string> r, IEnumerable<string> p) => throw new NotSupportedException();
    public string GeneratePlatformAccessToken(Guid u, string e, string? f, string? l, Guid t, string a, IEnumerable<string> r, IEnumerable<string> p, int m) => throw new NotSupportedException();
    public string GeneratePlatformAccessToken(Guid u, string e, string? f, string? l, Guid t, string a, IEnumerable<string> r, IEnumerable<string> p, int m, bool c) => throw new NotSupportedException();
    public string GenerateRefreshToken() => throw new NotSupportedException();
    public ClaimsPrincipal GetPrincipalFromExpiredToken(string token) => throw new NotSupportedException();
}
