using System.Diagnostics;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.AuthService.Application.Common.Entitlements;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Common.Services;

namespace Diten.AuthService.Api.Operational;

public sealed class EntitlementReconciliationOperationalRunner
{
    private readonly IEntitlementReconciliationOperationStore _store;
    private readonly ITokenService _tokens;
    private readonly ITenantEntitlementClient _source;
    private readonly Func<CancellationToken,Task<string>> _observeProcesses;
    public EntitlementReconciliationOperationalRunner(IEntitlementReconciliationOperationStore store,
        ITokenService tokens, ITenantEntitlementClient source)
        : this(store, tokens, source, ObserveProcessesAsync) { }
    public EntitlementReconciliationOperationalRunner(IEntitlementReconciliationOperationStore store,
        ITokenService tokens, ITenantEntitlementClient source, Func<CancellationToken,Task<string>> observeProcesses)
    { _store = store; _tokens = tokens; _source = source; _observeProcesses = observeProcesses; }

    public async Task<string> RunAsync(EntitlementReconciliationCommandOptions options, string token, CancellationToken ct)
    {
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(ct);
        bounded.CancelAfter(TimeSpan.FromMinutes(2));
        ct = bounded.Token;
        var deadline = DateTimeOffset.UtcNow.AddMinutes(2);
        var actorId = ValidateOperatorToken(token, deadline);
        var provenance = await VerifyProvenanceAsync(options, ct);
        using var mutex = new Semaphore(1, 1, "Diten.Auth.ProductIdentityEntitlementReconciliation");
        var owns = mutex.WaitOne(0);
        if (!owns) throw new InvalidOperationException("OPERATION_ALREADY_RUNNING");
        try
        {
            var processFingerprint = await _observeProcesses(ct);
            await _store.VerifyStorageAsync(ct);
            EntitlementReconciliationPlan? saved = null;
            if (options.Mode == "apply")
            {
                var text = await File.ReadAllTextAsync(options.PlanManifest!, ct);
                if (EntitlementReconciliationPlan.Hash(text) != options.PlanSha256) throw new InvalidOperationException("PLAN_HASH_MISMATCH");
                saved = JsonSerializer.Deserialize<EntitlementReconciliationPlan>(text)
                    ?? throw new InvalidOperationException("PLAN_INVALID");
                if (text != saved.CanonicalJson() || saved.OperationId != options.OperationId || saved.ActorId != actorId
                    || saved.TenantId != options.TenantId || saved.ModuleCode != options.ModuleCode
                    || saved.BinarySha256 != options.BinarySha256 || saved.ProvenanceSha256 != options.ProvenanceSha256
                    || saved.SourceHead != provenance.SourceHead) throw new InvalidOperationException("PLAN_BINDING_MISMATCH");
                var prior = await _store.ReadReceiptAsync(saved.OperationId, ct);
                if (prior is not null)
                    return await ReplayObservedAsync(saved, prior, token, deadline, processFingerprint, ct);
            }
            var local = await ReadAuthorizedLocalAsync(options.TenantId, actorId, ct);
            var authority = await _source.ReadReconciliationAuthorityAsync(options.TenantId, local.Operator.NormalizedEmail, ct);
            ValidateAuthority(local, authority);
            if (options.Mode == "plan")
            {
                var plan = EntitlementReconciliationPlanner.Build(local, authority, options.OperationId,
                    options.ProvenanceSha256, options.BinarySha256, provenance.SourceHead, DateTimeOffset.UtcNow);
                var fresh = await ReadAuthorizedLocalAsync(options.TenantId, actorId, ct);
                var freshAuthority = await _source.ReadReconciliationAuthorityAsync(options.TenantId, fresh.Operator.NormalizedEmail, ct);
                ValidateOperatorToken(token, deadline);
                if (fresh.Fingerprint != local.Fingerprint || fresh.Operator.Fingerprint != local.Operator.Fingerprint
                    || fresh.QuiescenceFingerprint != local.QuiescenceFingerprint || freshAuthority.Fingerprint != authority.Fingerprint
                    || await _observeProcesses(ct) != processFingerprint)
                    throw new InvalidOperationException("PLAN_OBSERVATION_DRIFT");
                await WritePlanAsync(options.PlanOutput!, plan, ct);
                return "PLAN " + options.PlanOutput + " " + plan.Sha256();
            }
            if (saved is null) throw new InvalidOperationException("APPLY_PLAN_REQUIRED");
            var expected = EntitlementReconciliationPlanner.Build(local, authority, saved.OperationId,
                saved.ProvenanceSha256, saved.BinarySha256, saved.SourceHead, saved.CreatedAtUtc);
            if (expected.Sha256() != saved.Sha256()) throw new InvalidOperationException("PLAN_PRECONDITION_DRIFT");
            // Last observations immediately precede store-owned transaction. These are observations, not a global fence.
            ValidateOperatorToken(token, deadline);
            var before = await ReadAuthorizedLocalAsync(saved.TenantId, actorId, ct);
            var sourceBefore = await _source.ReadReconciliationAuthorityAsync(saved.TenantId, before.Operator.NormalizedEmail, ct);
            ValidateAuthority(before, sourceBefore);
            if (before.Fingerprint != saved.LocalFingerprint || before.Operator.Fingerprint != saved.OperatorFingerprint
                || before.QuiescenceFingerprint != saved.QuiescenceFingerprint || sourceBefore.Fingerprint != saved.AuthorityFingerprint
                || await _observeProcesses(ct) != processFingerprint)
                throw new InvalidOperationException("PRECOMMIT_AUTHORITY_OR_LOCAL_DRIFT");
            var commit = await _store.CommitAsync(saved, ct);
            if (commit.Receipt is null) return commit.State;
            return await FinalizeAfterCommitAsync(saved, commit.Receipt, token, deadline, processFingerprint, ct);
        }
        finally { mutex.Release(); }
    }

    public Guid ValidateOperatorToken(string token, DateTimeOffset deadline)
    {
        var principal = _tokens.GetPrincipalFromCurrentToken(token, deadline);
        string Exact(string key)
        {
            var claims = principal.FindAll(key).ToArray();
            return claims.Length == 1 ? claims[0].Value : throw new InvalidOperationException("OPERATOR_CLAIM_AMBIGUOUS");
        }
        var sub = Exact("sub");
        var tenant = Exact("tenant_id");
        if (principal.Identity?.IsAuthenticated != true || !Guid.TryParseExact(sub, "D", out var id)
            || id == Guid.Empty || sub != id.ToString("D") || tenant != EntitlementReconciliationPlan.AdminTenant.ToString("D")
            || Exact("actor_type") != "platform_admin" || Exact("pwd_change_required") != "false"
            || principal.FindAll("permission").Count(c => c.Value == EntitlementReconciliationPlan.RequiredPermission) != 1
            || principal.HasClaim(c => c.Type == "tenantId")
            || principal.FindAll(ClaimTypes.NameIdentifier).Count() > 1
            || principal.FindAll(ClaimTypes.NameIdentifier).Any(c => c.Value != sub))
            throw new InvalidOperationException("OPERATOR_TOKEN_DENIED");
        return id;
    }

    private async Task<EntitlementLocalSnapshot> ReadAuthorizedLocalAsync(Guid tenant, Guid actor, CancellationToken ct)
    {
        var local = await _store.ReadAsync(tenant, actor, ct);
        if (local.TenantId != tenant || !local.Operator.IsAuthorized || local.Operator.UserId != actor
            || local.Operator.TenantId != EntitlementReconciliationPlan.AdminTenant
            || string.IsNullOrWhiteSpace(local.Operator.NormalizedEmail)
            || local.Operator.NormalizedEmail != local.Operator.NormalizedEmail.Trim().ToLowerInvariant())
            throw new InvalidOperationException("CURRENT_OPERATOR_DENIED");
        return local;
    }
    private static void ValidateAuthority(EntitlementLocalSnapshot local, EntitlementAuthoritySnapshot source)
    {
        if (!source.OperatorActive || !source.TenantActive || source.PermissionKeys.Count == 0 || source.RequestedTenantId != local.TenantId
            || source.ModuleCode != EntitlementReconciliationPlan.Module
            || source.NormalizedOperatorEmail != local.Operator.NormalizedEmail)
            throw new InvalidOperationException("CURRENT_PLATFORM_AUTHORITY_DENIED");
    }

    private async Task<string> ReplayObservedAsync(EntitlementReconciliationPlan plan, EntitlementOperationReceipt receipt,
        string token, DateTimeOffset deadline, string processes, CancellationToken ct)
    {
        if (receipt.PlanSha256 != plan.Sha256()) throw new InvalidOperationException("OPERATION_PLAN_CONFLICT");
        // Every exact replay observes current authority and complete post-state, even when an
        // immutable manual outcome already dominates or finalization was never recorded.
        var observation = await ObservePostCommitAsync(plan, receipt, token, deadline, processes, ct);
        var diagnostic = observation.Reason is null ? string.Empty : " CURRENT_OBSERVATION=" + observation.Reason;
        if (receipt.ManualHold || receipt.State == "COMMITTED_MANUAL_RECONCILIATION_REQUIRED")
            return "COMMITTED_MANUAL_RECONCILIATION_REQUIRED " + receipt.Reason + diagnostic;
        var reason = receipt.State == "SUCCESS" ? observation.Reason : "POST_COMMIT_FINALIZATION_MISSING";
        if (reason is null) return "SUCCESS REPLAY";
        try { await _store.FinalizeAsync(plan, false, reason, observation.Fingerprint, ct); }
        catch (Exception) { return "COMMITTED_MANUAL_RECONCILIATION_REQUIRED POST_COMMIT_FINALIZATION_MISSING" + diagnostic; }
        return "COMMITTED_MANUAL_RECONCILIATION_REQUIRED " + reason
            + (receipt.State == "SUCCESS" ? string.Empty : diagnostic);
    }

    private async Task<(string? Reason, string Fingerprint)> ObservePostCommitAsync(EntitlementReconciliationPlan plan, EntitlementOperationReceipt receipt,
        string token, DateTimeOffset deadline, string processes, CancellationToken ct)
    {
        // Same precedence for initial finalization and SUCCESS replay: operator, external, local.
        // Successful negative observations remain distinct from an unavailable observation.
        string? operatorReason = null;
        try
        {
            if (ValidateOperatorToken(token, deadline) != plan.ActorId)
                operatorReason = "OPERATOR_AUTHORITY_DRIFT_AFTER_COMMIT";
        }
        catch (Exception) { operatorReason = "OPERATOR_AUTHORITY_DRIFT_AFTER_COMMIT"; }
        EntitlementLocalSnapshot local;
        try { local = await _store.ReadAsync(plan.TenantId, plan.ActorId, ct); }
        catch (Exception) { return (operatorReason ?? "POST_COMMIT_AUTHORITY_UNAVAILABLE", receipt.PostStateFingerprint); }
        if (!local.Operator.IsAuthorized || local.Operator.UserId != plan.ActorId
            || local.Operator.TenantId != EntitlementReconciliationPlan.AdminTenant
            || local.Operator.Fingerprint != plan.OperatorFingerprint)
            operatorReason = "OPERATOR_AUTHORITY_DRIFT_AFTER_COMMIT";
        EntitlementAuthoritySnapshot source;
        try { source = await _source.ReadReconciliationAuthorityAsync(plan.TenantId, local.Operator.NormalizedEmail, ct); }
        catch (Exception) { return (operatorReason ?? "POST_COMMIT_AUTHORITY_UNAVAILABLE", local.Fingerprint); }
        if (!source.OperatorActive || source.NormalizedOperatorEmail != local.Operator.NormalizedEmail)
            operatorReason = "OPERATOR_AUTHORITY_DRIFT_AFTER_COMMIT";
        if (operatorReason is not null) return (operatorReason, local.Fingerprint);
        if (!source.TenantActive || source.RequestedTenantId != plan.TenantId || source.ModuleCode != plan.ModuleCode
            || source.PermissionKeys.Count == 0 || source.Fingerprint != plan.AuthorityFingerprint)
            return ("EXTERNAL_AUTHORITY_DRIFT_AFTER_COMMIT", local.Fingerprint);
        if (local.TenantId != plan.TenantId || local.Fingerprint != receipt.PostStateFingerprint
            || local.RoleAssignmentVersion != plan.ExpectedRoleAssignmentVersion + 1
            || local.QuiescenceFingerprint != plan.QuiescenceFingerprint)
            return ("LOCAL_CONCURRENCY_DRIFT_AFTER_COMMIT", local.Fingerprint);
        try
        {
            if (await _observeProcesses(ct) != processes)
                return ("LOCAL_CONCURRENCY_DRIFT_AFTER_COMMIT", local.Fingerprint);
        }
        catch (Exception) { return ("LOCAL_CONCURRENCY_DRIFT_AFTER_COMMIT", local.Fingerprint); }
        return (null, local.Fingerprint);
    }

    private async Task<string> FinalizeAfterCommitAsync(EntitlementReconciliationPlan plan, EntitlementOperationReceipt receipt,
        string token, DateTimeOffset deadline, string processes, CancellationToken ct)
    {
        var observation = await ObservePostCommitAsync(plan, receipt, token, deadline, processes, ct);
        try
        {
            var finalized = await _store.FinalizeAsync(plan, observation.Reason is null, observation.Reason, observation.Fingerprint, ct);
            return finalized.State == "SUCCESS" && !finalized.ManualHold ? "SUCCESS" :
                "COMMITTED_MANUAL_RECONCILIATION_REQUIRED " + finalized.Reason;
        }
        catch (Exception) { return "COMMITTED_MANUAL_RECONCILIATION_REQUIRED POST_COMMIT_FINALIZATION_MISSING"; }
    }

    private static async Task WritePlanAsync(string path, EntitlementReconciliationPlan plan, CancellationToken ct)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".pending";
        await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            var bytes = Encoding.UTF8.GetBytes(plan.CanonicalJson());
            await stream.WriteAsync(bytes, ct);
            stream.Flush(true);
        }
        File.Move(temporary, path, overwrite: false);
    }

    private sealed record ProvenanceFile(string Path, string Sha256);
    private sealed record Provenance(string SourceHead, IReadOnlyList<ProvenanceFile> SourceFiles, IReadOnlyList<ProvenanceFile> Binaries);
    private static readonly string[] RequiredRuntimeSources =
    [
        "services/Diten.AuthService/src/Diten.AuthService.Api/Program.cs",
        "services/Diten.AuthService/src/Diten.AuthService.Application/Common/Interfaces/IEntitlementPermissionSyncService.cs",
        "services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/EntitlementPermissionSyncService.cs",
        "services/Diten.AuthService/src/Diten.AuthService.Application/Common/Interfaces/ITenantEntitlementClient.cs",
        "services/Diten.AuthService/src/Diten.AuthService.Infrastructure/Services/PlatformTenantEntitlementClient.cs",
        "services/Diten.AuthService/src/Diten.AuthService.Application/Common/Interfaces/ITokenService.cs",
        "services/Diten.AuthService/src/Diten.AuthService.Infrastructure/Services/TokenService.cs",
        "services/Diten.AuthService/src/Diten.AuthService.Persistence/DependencyInjection.cs",
        "services/Diten.AuthService/src/Diten.AuthService.Application/Common/Entitlements/EntitlementReconciliationPlan.cs",
        "services/Diten.AuthService/src/Diten.AuthService.Application/Common/Interfaces/IEntitlementReconciliationOperationStore.cs",
        "services/Diten.AuthService/src/Diten.AuthService.Api/Operational/EntitlementReconciliationOperationalMode.cs",
        "services/Diten.AuthService/src/Diten.AuthService.Api/Operational/EntitlementReconciliationCommandOptions.cs",
        "services/Diten.AuthService/src/Diten.AuthService.Api/Operational/EntitlementReconciliationOperationalRunner.cs",
        "services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/EntitlementReconciliationOperationStore.cs",
    ];
    private static async Task<Provenance> VerifyProvenanceAsync(EntitlementReconciliationCommandOptions options, CancellationToken ct)
    {
        var bytes = await File.ReadAllBytesAsync(options.ProvenanceManifest, ct);
        if (Convert.ToHexString(SHA256.HashData(bytes)) != options.ProvenanceSha256) throw new InvalidOperationException("PROVENANCE_HASH_MISMATCH");
        var manifest = JsonSerializer.Deserialize<Provenance>(bytes) ?? throw new InvalidOperationException("PROVENANCE_INVALID");
        if (manifest.SourceHead.Length != 40 || !manifest.SourceHead.All(Uri.IsHexDigit)
            || manifest.SourceFiles.Count == 0 || manifest.Binaries.Count == 0)
            throw new InvalidOperationException("PROVENANCE_INCOMPLETE");
        using var git = new Process { StartInfo = new ProcessStartInfo
        {
            FileName = "git", UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        }};
        git.StartInfo.ArgumentList.Add("--no-optional-locks");
        git.StartInfo.ArgumentList.Add("rev-parse");
        git.StartInfo.ArgumentList.Add("HEAD");
        git.StartInfo.ArgumentList.Add("--show-toplevel");
        if (!git.Start()) throw new InvalidOperationException("SOURCE_HEAD_UNAVAILABLE");
        var actualHead = git.StandardOutput.ReadToEndAsync(ct);
        var gitError = git.StandardError.ReadToEndAsync(ct);
        await git.WaitForExitAsync(ct);
        _ = await gitError;
        var checkout = (await actualHead).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (git.ExitCode != 0 || checkout.Length != 2 || checkout[0] != manifest.SourceHead)
            throw new InvalidOperationException("SOURCE_HEAD_MISMATCH");
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(checkout[1]));
        var sourcePaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var source in manifest.SourceFiles)
        {
            if (!Path.IsPathFullyQualified(source.Path) || source.Path != Path.GetFullPath(source.Path)
                || !source.Path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                || !sourcePaths.Add(source.Path)) throw new InvalidOperationException("SOURCE_PATH_NOT_CANONICAL");
            // Reject filesystem aliases as well as textual dot/parent/alternate-root substitutions.
            for (var current = source.Path; current is not null; current = Path.GetDirectoryName(current))
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("SOURCE_PATH_ALIAS_FORBIDDEN");
                if (current == root) break;
            }
        }
        if (RequiredRuntimeSources.Any(relative => !sourcePaths.Contains(Path.GetFullPath(Path.Combine(root,
            relative.Replace('/', Path.DirectorySeparatorChar))))))
            throw new InvalidOperationException("RUNTIME_SOURCE_CHAIN_INCOMPLETE");
        var entries = manifest.SourceFiles.Concat(manifest.Binaries).ToArray();
        if (entries.Select(e => e.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count() != entries.Length)
            throw new InvalidOperationException("PROVENANCE_DUPLICATE");
        foreach (var file in entries)
        {
            if (!Path.IsPathFullyQualified(file.Path) || file.Sha256.Length != 64
                || Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(file.Path, ct))) != file.Sha256.ToUpperInvariant())
                throw new InvalidOperationException("PROVENANCE_FILE_MISMATCH");
        }
        var binary = typeof(EntitlementReconciliationOperationalRunner).Assembly.Location;
        var binaryHash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(binary, ct)));
        if (binaryHash != options.BinarySha256 || !manifest.Binaries.Any(f => Path.GetFullPath(f.Path) == Path.GetFullPath(binary)
            && f.Sha256.ToUpperInvariant() == binaryHash)) throw new InvalidOperationException("BINARY_PROVENANCE_MISMATCH");
        foreach (var name in new[] { "Diten.AuthService.Api.dll", "Diten.AuthService.Application.dll",
            "Diten.AuthService.Domain.dll", "Diten.AuthService.Infrastructure.dll", "Diten.AuthService.Persistence.dll" })
        {
            var path = Path.Combine(Path.GetDirectoryName(binary)!, name);
            if (!manifest.Binaries.Any(f => Path.GetFullPath(f.Path) == Path.GetFullPath(path)))
                throw new InvalidOperationException("BINARY_CHAIN_INCOMPLETE");
        }
        return manifest;
    }

    // Actual OS observations, not an "I stopped the host" input. No commandline or owner is printed.
    public static async Task<string> ObserveProcessesAsync(CancellationToken ct)
    {
        if (!OperatingSystem.IsWindows()) throw new InvalidOperationException("WINDOWS_PROCESS_OBSERVATION_REQUIRED");
        using var process = new Process { StartInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe", UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        }};
        process.StartInfo.ArgumentList.Add("-NoProfile");
        process.StartInfo.ArgumentList.Add("-NonInteractive");
        process.StartInfo.ArgumentList.Add("-Command");
        process.StartInfo.ArgumentList.Add(
            "$ErrorActionPreference='Stop'; $self=" + Environment.ProcessId +
            "; $rows=@(Get-CimInstance Win32_Process | Where-Object { $_.ProcessId -ne $self -and $_.ProcessId -ne $PID -and ($_.Name -like 'Diten.AuthService*' -or $_.CommandLine -match 'Diten.AuthService') });" +
            "foreach($row in $rows){ $owner=Invoke-CimMethod -InputObject $row -MethodName GetOwner; if($owner.ReturnValue -ne 0){exit 4} }; if($rows.Count -ne 0){exit 3};" +
            "$listeners=@(Get-NetTCPConnection -State Listen -ErrorAction Stop); if(@($listeners | Where-Object LocalPort -eq 5056).Count -ne 0){exit 3}; 'AUTH_WRITERS_ABSENT'");
        if (!process.Start()) throw new InvalidOperationException("PROCESS_OBSERVATION_FAILED");
        var output = process.StandardOutput.ReadToEndAsync(ct);
        var error = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        var text = (await output).Trim();
        _ = await error;
        if (process.ExitCode != 0 || text != "AUTH_WRITERS_ABSENT")
            throw new InvalidOperationException("AUTH_WRITERS_NOT_QUIESCENT");
        return EntitlementReconciliationPlan.Hash(text);
    }
}
