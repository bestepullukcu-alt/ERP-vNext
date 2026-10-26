using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Xunit;
using Call = Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.ProductIdentityWorkflowCall;
using Kind = Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.ProductIdentityWorkflowFaultKind;
using Outcome = Diten.MdmService.Application.Contracts.Workflow.ProductIdentityWorkflowTransportOutcome;
using Scope = Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.ProductIdentityWorkflowFaultScope;

namespace Diten.MdmService.Application.Tests.Workflow;

// The classification every product-identity processor is meant to share (WP-GP-1B-A-RESILIENCE-01). Today only the
// Global Product identity processor asks it.
public sealed class ProductIdentityWorkflowFaultPolicyTests
{
    [Theory]
    // service chain → wait
    [InlineData(Call.TerminalEvidence, Outcome.Retryable, "ANYTHING", Kind.ServiceWait, Scope.None, false)]
    [InlineData(Call.TerminalEvidence, Outcome.Timeout, "ANYTHING", Kind.ServiceWait, Scope.None, false)]
    [InlineData(Call.TerminalEvidence, Outcome.AuthenticationRejected, "PRODUCT_WORKFLOW_IDENTITY_REJECTED", Kind.ServiceWait, Scope.AllTenants, true)]
    [InlineData(Call.TerminalEvidence, Outcome.AuthenticationRejected, "PRODUCT_WORKFLOW_IDENTITY_NOT_GRANTED", Kind.ServiceWait, Scope.Tenant, true)]
    [InlineData(Call.TerminalEvidence, Outcome.AuthenticationRejected, "WORKFLOW_TRUSTED_CONSUMER_UNAUTHENTICATED", Kind.ServiceWait, Scope.AllTenants, true)]
    [InlineData(Call.TerminalEvidence, Outcome.AuthenticationRejected, null, Kind.ServiceWait, Scope.AllTenants, true)]
    [InlineData(Call.Start, Outcome.Forbidden, "WORKFLOW_TRUSTED_CONSUMER_FORBIDDEN", Kind.ServiceWait, Scope.AllTenants, true)]
    [InlineData(Call.Start, Outcome.Forbidden, "WORKFLOW_TRUSTED_START_FORBIDDEN", Kind.ServiceWait, Scope.None, true)]
    [InlineData(Call.Start, Outcome.Forbidden, null, Kind.ServiceWait, Scope.None, true)]
    [InlineData(Call.Cancel, Outcome.Invalid, "PRODUCT_WORKFLOW_RESPONSE_UNREADABLE", Kind.ServiceWait, Scope.None, false)]
    [InlineData(Call.Start, Outcome.Invalid, "PRODUCT_WORKFLOW_RESPONSE_INVALID", Kind.ServiceWait, Scope.None, false)]
    // the maker's, or the set-up's
    [InlineData(Call.Start, Outcome.AuthenticationRejected, "WORKFLOW_DELEGATED_USER_UNAUTHENTICATED", Kind.MakerReplay, Scope.None, true)]
    [InlineData(Call.Start, Outcome.Forbidden, "WORKFLOW_DELEGATED_USER_FORBIDDEN", Kind.MakerReplay, Scope.None, true)]
    [InlineData(Call.Start, Outcome.Invalid, "WORKFLOW_ASSIGNMENT_CANDIDATES_REQUIRED", Kind.MakerReplay, Scope.None, true)]
    [InlineData(Call.Start, Outcome.NotFound, "NOT_FOUND_NON_LEAKAGE", Kind.MakerReplay, Scope.None, true)]
    [InlineData(Call.Start, Outcome.Conflict, "WORKFLOW_TEMPLATE_NOT_PUBLISHED", Kind.MakerReplay, Scope.None, true)]
    // WP-MDM-NO-ELIGIBLE-APPROVER-01: segregation of duties — only a START can meet it
    [InlineData(Call.Start, Outcome.Invalid, "WORKFLOW_NO_ELIGIBLE_APPROVER", Kind.MakerReplay, Scope.None, true)]
    // the record's
    [InlineData(Call.Start, Outcome.Forbidden, "WORKFLOW_TRUSTED_CONSUMER_TENANT_MISMATCH", Kind.Quarantine, Scope.None, false)]
    [InlineData(Call.Cancel, Outcome.Forbidden, "WORKFLOW_TRUSTED_CANCEL_REQUESTER_FORBIDDEN", Kind.Quarantine, Scope.None, false)]
    [InlineData(Call.Start, Outcome.Invalid, "PRODUCT_WORKFLOW_FAILURE_WITHOUT_REASON", Kind.Quarantine, Scope.None, false)]
    [InlineData(Call.Start, Outcome.Invalid, null, Kind.Quarantine, Scope.None, false)]
    [InlineData(Call.Start, Outcome.Conflict, "WORKFLOW_START_IDEMPOTENCY_CONFLICT", Kind.Quarantine, Scope.None, false)]
    [InlineData(Call.Start, Outcome.Conflict, null, Kind.Quarantine, Scope.None, false)]
    // a definition that is not ready is only ever a START's answer
    [InlineData(Call.StartResult, Outcome.Invalid, "WORKFLOW_NO_ELIGIBLE_APPROVER", Kind.Quarantine, Scope.None, false)]
    [InlineData(Call.TerminalEvidence, Outcome.Invalid, "WORKFLOW_NO_ELIGIBLE_APPROVER", Kind.Quarantine, Scope.None, false)]
    [InlineData(Call.Cancel, Outcome.Invalid, "WORKFLOW_NO_ELIGIBLE_APPROVER", Kind.Quarantine, Scope.None, false)]
    [InlineData(Call.TerminalEvidence, Outcome.Conflict, "WORKFLOW_TEMPLATE_NOT_PUBLISHED", Kind.Quarantine, Scope.None, false)]
    public void Classify_decides_by_outcome_and_code(
        Call call, Outcome outcome, string? code, Kind kind, Scope scope, bool startedNothing)
    {
        var decision = ProductIdentityWorkflowFaultPolicy.Classify(call, outcome, code);

        Assert.Equal(kind, decision.Kind);
        Assert.Equal(scope, decision.Scope);
        Assert.Equal(startedNothing, decision.StartedNothing);
        Assert.Equal(kind == Kind.MakerReplay, decision.MakerCode is not null);
        // Only a success that breaks the contract is said as an error at once: waiting will not mend it.
        Assert.Equal(code == "PRODUCT_WORKFLOW_RESPONSE_INVALID", decision.Loud);
    }

    [Theory]
    [InlineData(1, 30, 900, 30)]
    [InlineData(2, 30, 900, 60)]
    [InlineData(5, 30, 900, 480)]
    [InlineData(6, 30, 900, 900)]
    [InlineData(60, 30, 900, 900)]
    [InlineData(int.MaxValue, 1, 900, 900)]
    [InlineData(1, 3600, 900, 900)]
    [InlineData(0, 30, 900, 30)]
    public void Backoff_doubles_and_never_passes_the_ceiling(int faults, int retrySeconds, int ceilingSeconds, int expected)
    {
        Assert.Equal(
            TimeSpan.FromSeconds(expected),
            ProductIdentityWorkflowFaultPolicy.Backoff(
                faults, TimeSpan.FromSeconds(retrySeconds), TimeSpan.FromSeconds(ceilingSeconds)));
    }

    [Fact]
    public void Maker_codes_are_a_fixed_contract()
    {
        // The screen (GP-2) writes its sentences against these exact strings and statuses.
        Assert.Equal("PRODUCT_IDENTITY_WORKFLOW_SESSION_EXPIRED", ProductIdentityWorkflowMakerCodes.SessionExpired);
        Assert.Equal("PRODUCT_IDENTITY_WORKFLOW_START_PERMISSION_REQUIRED", ProductIdentityWorkflowMakerCodes.StartPermissionRequired);
        Assert.Equal("PRODUCT_IDENTITY_WORKFLOW_APPROVER_NOT_ASSIGNED", ProductIdentityWorkflowMakerCodes.ApproverNotAssigned);
        Assert.Equal("PRODUCT_IDENTITY_WORKFLOW_DEFINITION_NOT_READY", ProductIdentityWorkflowMakerCodes.DefinitionNotReady);
        Assert.Equal("PRODUCT_IDENTITY_WORKFLOW_SERVICE_UNAVAILABLE_RESUBMIT", ProductIdentityWorkflowMakerCodes.ServiceUnavailableResubmit);
        Assert.Equal("PRODUCT_IDENTITY_WORKFLOW_MAKER_REPLAY_REQUIRED", ProductIdentityWorkflowMakerCodes.MakerReplayRequired);
        Assert.Equal(401, ProductIdentityWorkflowMakerCodes.StatusFor(ProductIdentityWorkflowMakerCodes.SessionExpired));
        Assert.Equal(403, ProductIdentityWorkflowMakerCodes.StatusFor(ProductIdentityWorkflowMakerCodes.StartPermissionRequired));
        Assert.Equal(409, ProductIdentityWorkflowMakerCodes.StatusFor(ProductIdentityWorkflowMakerCodes.ApproverNotAssigned));
        Assert.Equal(409, ProductIdentityWorkflowMakerCodes.StatusFor(ProductIdentityWorkflowMakerCodes.DefinitionNotReady));
        Assert.Equal(503, ProductIdentityWorkflowMakerCodes.StatusFor(ProductIdentityWorkflowMakerCodes.ServiceUnavailableResubmit));
        Assert.Equal(409, ProductIdentityWorkflowMakerCodes.StatusFor(ProductIdentityWorkflowMakerCodes.MakerReplayRequired));
        // WP-MDM-NO-ELIGIBLE-APPROVER-01
        Assert.Equal("PRODUCT_IDENTITY_WORKFLOW_NO_ELIGIBLE_APPROVER", ProductIdentityWorkflowMakerCodes.NoEligibleApprover);
        Assert.Equal(409, ProductIdentityWorkflowMakerCodes.StatusFor(ProductIdentityWorkflowMakerCodes.NoEligibleApprover));
        Assert.True(ProductIdentityWorkflowMakerCodes.IsMakerCode(ProductIdentityWorkflowMakerCodes.NoEligibleApprover));
    }

    // WP-MDM-NO-ELIGIBLE-APPROVER-01: the start Platform refuses because the maker would approve their own record
    // becomes the maker's code — nothing was started, the record is not quarantined.
    [Fact]
    public void A_start_refused_for_no_eligible_approver_is_the_makers_with_nothing_started()
    {
        var decision = ProductIdentityWorkflowFaultPolicy.Classify(Call.Start, Outcome.Invalid, "WORKFLOW_NO_ELIGIBLE_APPROVER");

        Assert.Equal(Kind.MakerReplay, decision.Kind);
        Assert.True(decision.StartedNothing);
        Assert.Equal(ProductIdentityWorkflowMakerCodes.NoEligibleApprover, decision.MakerCode);
        Assert.Equal(Scope.None, decision.Scope);
    }
}
