using Diten.FpaService.Application.Modules.Budgeting.Contracts;
using Diten.FpaService.Domain.Modules.Budgeting.Selections;

namespace Diten.FpaService.Application.Modules.Budgeting.GateI;

public static class BudgetGateIProfile
{
    public const string Operation = "budgeting.budget-version-references.validate";
    public const string Permission = Operation;
    public const string Audience = "diten-fpa-service";
    public const string ClientId = "diten.fpa";
    public const string Scope = "diten.s2s.delegated.invoke";
}

public sealed record BudgetGateITrustedContext(bool Authenticated, Guid TenantId, Guid ActorId, string ClientId,
    string Audience, string Scope, string Operation, string Permission, string RequestHash);
public interface IBudgetGateITrustedContextAccessor { BudgetGateITrustedContext Current { get; } }
public sealed record BudgetVersionSnapshot(bool Exists, Guid TenantId, Guid BudgetId, Guid VersionId, long Number,
    bool Visible, bool Retired, bool IsCurrentForInvestmentCase);
public interface IBudgetVersionSnapshotReader
{
    Task<BudgetVersionSnapshot?> ReadAsync(Guid tenantId, Guid budgetId, Guid versionId, Guid investmentCaseId, CancellationToken cancellationToken);
}
public sealed record BudgetProviderOptions(bool Enabled) { public static BudgetProviderOptions Disabled { get; } = new(false); }
public sealed record BudgetValidationResponse(int StatusCode, string Code, BudgetVersionReferenceV1? Reference = null,
    bool Resolved = false, bool Eligible = false, bool Current = false, bool Retired = false);

public sealed class BudgetVersionReferenceValidationProvider(IBudgetVersionSnapshotReader reader, BudgetProviderOptions options)
{
    public async Task<BudgetValidationResponse> ValidateAsync(BudgetVersionValidationRequest request,
        BudgetGateITrustedContext context, CancellationToken cancellationToken)
    {
        if (!options.Enabled) return new(503, "budget_reference_provider_disabled");
        if (request.BudgetVersionReference is null || request.InvestmentCaseContext is null)
            return new(400, "budget_reference_request_malformed");
        if (!context.Authenticated || context.TenantId == Guid.Empty || context.ActorId == Guid.Empty ||
            string.IsNullOrWhiteSpace(context.RequestHash)) return new(401, "budget_reference_context_invalid");
        if (!Exact(context)) return new(403, "budget_reference_forbidden");
        BudgetVersionSnapshot? snapshot;
        try
        {
            snapshot = await reader.ReadAsync(context.TenantId, request.BudgetVersionReference.BudgetId,
                request.BudgetVersionReference.BudgetVersionId, request.InvestmentCaseContext.InvestmentCaseId,
                cancellationToken);
        }
        catch (OperationCanceledException) { throw; }
        catch { return new(503, "budget_reference_authority_unavailable"); }
        if (snapshot is null || !snapshot.Exists || !snapshot.Visible || snapshot.TenantId != context.TenantId ||
            snapshot.BudgetId != request.BudgetVersionReference.BudgetId ||
            snapshot.VersionId != request.BudgetVersionReference.BudgetVersionId ||
            snapshot.Number != request.BudgetVersionReference.BudgetVersionNumber)
            return new(404, "budget_reference_not_found");
        if (request.Mode == BudgetVersionValidationMode.NewReferenceEligibility && snapshot.Retired)
            return new(409, "budget_reference_ineligible", request.BudgetVersionReference, true, false, false, true);
        if (request.Mode == BudgetVersionValidationMode.CurrentSelectionEligibility && !snapshot.IsCurrentForInvestmentCase)
            return new(409, "budget_reference_not_current", request.BudgetVersionReference, true, true, false, snapshot.Retired);
        return new(200, "budget_reference_accepted", request.BudgetVersionReference, true, !snapshot.Retired,
            snapshot.IsCurrentForInvestmentCase, snapshot.Retired);
    }
    private static bool Exact(BudgetGateITrustedContext x) =>
        string.Equals(x.ClientId, BudgetGateIProfile.ClientId, StringComparison.Ordinal) &&
        string.Equals(x.Audience, BudgetGateIProfile.Audience, StringComparison.Ordinal) &&
        string.Equals(x.Scope, BudgetGateIProfile.Scope, StringComparison.Ordinal) &&
        string.Equals(x.Operation, BudgetGateIProfile.Operation, StringComparison.Ordinal) &&
        string.Equals(x.Permission, BudgetGateIProfile.Permission, StringComparison.Ordinal);
}
public sealed class UnavailableBudgetVersionSnapshotReader : IBudgetVersionSnapshotReader
{
    public Task<BudgetVersionSnapshot?> ReadAsync(Guid tenantId, Guid budgetId, Guid versionId, Guid investmentCaseId, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Budgeting persistence reader is not composed.");
}
public sealed class UnavailableBudgetGateITrustedContextAccessor : IBudgetGateITrustedContextAccessor
{
    public BudgetGateITrustedContext Current { get; } = new(false, Guid.Empty, Guid.Empty, "", "", "", "", "", "");
}
