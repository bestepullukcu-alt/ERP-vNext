using Diten.AuthService.Application.Common.Entitlements;
namespace Diten.AuthService.Api.Operational;

public sealed record EntitlementReconciliationCommandOptions(string Mode, Guid TenantId, string ModuleCode,
    Guid OperationId, string ProvenanceManifest, string ProvenanceSha256, string BinarySha256,
    string? PlanOutput, string? PlanManifest, string? PlanSha256)
{
    public const string Selector = "--run-product-identity-entitlement-reconciliation";
    public static EntitlementReconciliationCommandOptions Parse(string[] args)
    {
        if (args.Length < 1 || args[0] != Selector) throw new InvalidOperationException("EXACT_SELECTOR_REQUIRED");
        var allowed = new HashSet<string>(StringComparer.Ordinal) { "--mode", "--tenant-id", "--module-code",
            "--operation-id", "--provenance-manifest", "--expected-provenance-sha256", "--expected-binary-sha256",
            "--plan-output", "--plan-manifest", "--expected-plan-sha256", "--expected-add-count",
            "--expected-remove-count", "--expected-remove-grant-id" };
        var values = new Dictionary<string,string>(StringComparer.Ordinal);
        for (var i = 1; i < args.Length; i += 2)
            if (i + 1 >= args.Length || !allowed.Contains(args[i]) || !values.TryAdd(args[i], args[i+1]))
                throw new InvalidOperationException("UNKNOWN_DUPLICATE_OR_INCOMPLETE_ARGUMENT");
        string Required(string key) => values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value : throw new InvalidOperationException("REQUIRED_ARGUMENT_MISSING");
        Guid Id(string key) => Guid.TryParseExact(Required(key), "D", out var id) && id != Guid.Empty
            ? id : throw new InvalidOperationException("CANONICAL_ID_REQUIRED");
        string PathValue(string key)
        {
            var value = Required(key);
            if (!Path.IsPathFullyQualified(value) || value != Path.GetFullPath(value))
                throw new InvalidOperationException("ABSOLUTE_CANONICAL_PATH_REQUIRED");
            return value;
        }
        string Hash(string key)
        {
            var value = Required(key);
            if (value.Length != 64 || !value.All(Uri.IsHexDigit)) throw new InvalidOperationException("SHA256_REQUIRED");
            return value.ToUpperInvariant();
        }
        var mode = Required("--mode");
        var tenant = Id("--tenant-id");
        if (tenant != EntitlementReconciliationPlan.TargetTenant || Required("--module-code") != EntitlementReconciliationPlan.Module)
            throw new InvalidOperationException("EXACT_TARGET_REQUIRED");
        string? output = null, manifest = null, planHash = null;
        if (mode == "plan")
        {
            if (values.Count != 8) throw new InvalidOperationException("PLAN_ARGUMENT_SET_INVALID");
            output = PathValue("--plan-output");
            if (File.Exists(output)) throw new InvalidOperationException("PLAN_OUTPUT_EXISTS");
        }
        else if (mode == "apply")
        {
            if (values.Count != 12 || Required("--expected-add-count") != "6" || Required("--expected-remove-count") != "1"
                || Id("--expected-remove-grant-id") != EntitlementReconciliationPlan.RemovedGrant)
                throw new InvalidOperationException("APPLY_DELTA_ARGUMENT_SET_INVALID");
            manifest = PathValue("--plan-manifest");
            planHash = Hash("--expected-plan-sha256");
        }
        else throw new InvalidOperationException("MODE_INVALID");
        return new(mode, tenant, EntitlementReconciliationPlan.Module, Id("--operation-id"),
            PathValue("--provenance-manifest"), Hash("--expected-provenance-sha256"),
            Hash("--expected-binary-sha256"), output, manifest, planHash);
    }
}
