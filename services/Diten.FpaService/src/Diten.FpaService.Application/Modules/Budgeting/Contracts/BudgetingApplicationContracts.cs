using System.Security.Cryptography;
using System.Text;
using Diten.FpaService.Domain.Modules.Budgeting.Selections;

namespace Diten.FpaService.Application.Modules.Budgeting.Contracts;

public enum BudgetVersionValidationMode { HistoricalResolve, NewReferenceEligibility, CurrentSelectionEligibility }

public sealed record BudgetVersionValidationRequest(
    BudgetVersionReferenceV1 BudgetVersionReference,
    BudgetVersionValidationMode Mode,
    InvestmentCaseContextV1? InvestmentCaseContext);

public sealed record BudgetVersionValidationResult(int StatusCode, bool Accepted, string Code)
{
    public static BudgetVersionValidationResult Malformed => new(400, false, "malformed");
    public static BudgetVersionValidationResult Unauthenticated => new(401, false, "unauthenticated");
    public static BudgetVersionValidationResult Forbidden => new(403, false, "forbidden");
    public static BudgetVersionValidationResult NotFound => new(404, false, "not-found");
    public static BudgetVersionValidationResult Conflict => new(409, false, "conflict");
    public static BudgetVersionValidationResult Unavailable => new(503, false, "unavailable");
    public static BudgetVersionValidationResult Success => new(200, true, "accepted");
}

public sealed class SelectedBudgetVersionReferenceV1
{
    public const string ExpectedContractName = "ppm.investment-case-selected-budget-version-reference";
    public const string ExpectedContractVersion = "1.0";
    public SelectedBudgetVersionReferenceV1(string name,string version,InvestmentCaseContextV1 context,BudgetVersionReferenceV1 reference){if(name!=ExpectedContractName||version!=ExpectedContractVersion)throw new ArgumentException("unsupported wrapper identity");ContractName=name;ContractVersion=version;InvestmentCaseContext=context;BudgetVersionReference=reference;}
    public string ContractName{get;}public string ContractVersion{get;}public InvestmentCaseContextV1 InvestmentCaseContext{get;}public BudgetVersionReferenceV1 BudgetVersionReference{get;}
}

public static class BudgetingPermissions
{
    public static IReadOnlyList<(string Operation, string Permission)> ExactManifest { get; } =
    [
        ("budgeting.budgets.read", "budgeting.budgets.read"),
        ("budgeting.budgets.create", "budgeting.budgets.create"),
        ("budgeting.budgets.update", "budgeting.budgets.update"),
        ("budgeting.budgets.archive", "budgeting.budgets.archive"),
        ("budgeting.budget-version-drafts.read", "budgeting.budget-version-drafts.read"),
        ("budgeting.budget-version-drafts.create", "budgeting.budget-version-drafts.create"),
        ("budgeting.budget-version-drafts.update", "budgeting.budget-version-drafts.update"),
        ("budgeting.budget-version-drafts.abandon", "budgeting.budget-version-drafts.abandon"),
        ("budgeting.budget-versions.read", "budgeting.budget-versions.read"),
        ("budgeting.budget-versions.certify", "budgeting.budget-versions.certify"),
        ("budgeting.budget-versions.retire", "budgeting.budget-versions.retire"),
        ("budgeting.funding-baseline-selections.read", "budgeting.funding-baseline-selections.read"),
        ("budgeting.funding-baseline-selections.replace", "budgeting.funding-baseline-selections.replace"),
        ("budgeting.funding-baseline-selections.close", "budgeting.funding-baseline-selections.close"),
        ("budgeting.budget-version-references.validate", "budgeting.budget-version-references.validate")
    ];
}

public static class BudgetingIdempotency
{
    public static bool IsValidKey(string? key) => key is { Length: >= 1 and <= 128 } &&
        key.All(c => c <= 127 && (char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or ':' or '-'));

    public static string PayloadHash(ReadOnlySpan<byte> canonicalRequestBytes) =>
        Convert.ToHexString(SHA256.HashData(canonicalRequestBytes)).ToLowerInvariant();

    public static IdempotencyDecision Decide(string expectedHash, ReceiptState receipt) => receipt switch
    {
        ReceiptState.Found found when found.PayloadHash == expectedHash => new(200, false, found.StableResult),
        ReceiptState.Found => new(409, false, null),
        ReceiptState.Absent or ReceiptState.Indeterminate => new(503, false, null),
        _ => throw new ArgumentOutOfRangeException(nameof(receipt))
    };
}

public abstract record ReceiptState
{
    public sealed record Found(string PayloadHash, string StableResult) : ReceiptState;
    public sealed record Absent : ReceiptState;
    public sealed record Indeterminate : ReceiptState;
}

public sealed record IdempotencyDecision(int StatusCode, bool ExecuteBody, string? StableResult);

public enum UnknownCommitAction { RetryCommitOnSameSession, ReconcileReceipt, ReturnConflict, ReturnUnavailable, ReturnStoredResult }

public static class UnknownCommitPolicy
{
    public static UnknownCommitAction Decide(bool commitRetriesRemain, string expectedPayloadHash, ReceiptState? receipt = null) =>
        commitRetriesRemain ? UnknownCommitAction.RetryCommitOnSameSession : receipt switch
        {
            ReceiptState.Found found when found.PayloadHash == expectedPayloadHash => UnknownCommitAction.ReturnStoredResult,
            ReceiptState.Found => UnknownCommitAction.ReturnConflict,
            ReceiptState.Absent or ReceiptState.Indeterminate or null => UnknownCommitAction.ReturnUnavailable,
            _ => throw new ArgumentOutOfRangeException(nameof(receipt))
        };
}

public static class ProductionSigningIdentityPolicy
{
    public const string FixtureIdentity="diten.fpa.budgeting.audit.fixture-test";
    public const string FixtureKeyId="budgeting-mod-0136-fixture-current.test-only";
    public static bool Reject(bool testOnly,string identity,string keyId,bool fixtureSecretDerivedSignature) =>
        testOnly || identity==FixtureIdentity || keyId==FixtureKeyId || fixtureSecretDerivedSignature;
}
