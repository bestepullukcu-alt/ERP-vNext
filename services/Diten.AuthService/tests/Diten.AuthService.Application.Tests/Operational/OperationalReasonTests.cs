using Diten.AuthService.Api.Operational;
using Diten.AuthService.Api.Services.ServiceIdentityTokens;

namespace Diten.AuthService.Application.Tests.Operational;

/// <summary>
/// WP-AUTH-BL570-01 FIX2 item 2 — the operator SEES why an operational command refused (the live Windows measurement
/// rests on these names). Only fixed codes reach stdout/stderr; a message that is not a code never does.
/// </summary>
public sealed class OperationalReasonTests
{
    [Theory]
    [InlineData("OPERATION_ALREADY_RUNNING")]
    [InlineData("OPERATION_LOCK_HELD_BY_ANOTHER_ACCOUNT")]
    [InlineData("OPERATION_LOCK_UNAVAILABLE")]
    [InlineData("AUTH_WRITERS_NOT_QUIESCENT")]
    [InlineData("PROCESS_OBSERVATION_FAILED")]
    [InlineData("PROCESS_OBSERVATION_UNSUPPORTED_PLATFORM")]
    [InlineData("PLAN_HASH_MISMATCH")]
    [InlineData("DEVELOPMENT_PROCESS_PROVENANCE_REQUIRED")]
    public void The_reconciliation_command_names_its_refusal_on_stderr(string code)
    {
        Assert.Equal("ENTITLEMENT_OPERATION_FAILED_CLOSED reason=" + code,
            EntitlementReconciliationOperationalMode.FailureLine(new InvalidOperationException(code)));
    }

    [Theory]
    [InlineData("Bearer eyJhbGciOiJIUzI1NiJ9.e30.x")]
    [InlineData("mongodb://user:password@127.0.0.1:27017/db")]
    [InlineData("The process cannot access the file '/srv/auth/plan.json'.")]
    [InlineData("SENSITIVE-MESSAGE")]
    public void A_message_that_is_not_a_fixed_code_never_reaches_the_operator(string message)
    {
        var line = EntitlementReconciliationOperationalMode.FailureLine(new InvalidOperationException(message));
        Assert.Equal("ENTITLEMENT_OPERATION_FAILED_CLOSED", line);
        Assert.DoesNotContain(message, line, StringComparison.Ordinal);
    }

    // The real lock refusal travels to the line unchanged.
    [Fact]
    public void A_real_lock_refusal_reaches_the_line_by_name()
    {
        var refused = Assert.Throws<InvalidOperationException>(() =>
            OperationalSingleInstanceLock.TryAcquire("Diten.Auth.Test.Reason." + Guid.NewGuid().ToString("N"),
                _ => throw new UnauthorizedAccessException("denied")));
        Assert.Equal("ENTITLEMENT_OPERATION_FAILED_CLOSED reason=OPERATION_LOCK_HELD_BY_ANOTHER_ACCOUNT",
            EntitlementReconciliationOperationalMode.FailureLine(refused));
    }

    [Theory]
    [InlineData(ServiceClientSecretOutputSink.Reasons.NotWriteOnly)]
    [InlineData(ServiceClientSecretOutputSink.Reasons.NotAFifo)]
    [InlineData(ServiceClientSecretOutputSink.Reasons.SameAsStandardStream)]
    [InlineData(ServiceClientSecretOutputSink.Reasons.FifoTooPermissive)]
    public void A_secret_channel_refusal_carries_its_reason(string reason)
    {
        Assert.Equal(reason, OperationalReason.From(new SecretChannelRejectedException(reason)));
    }
}
