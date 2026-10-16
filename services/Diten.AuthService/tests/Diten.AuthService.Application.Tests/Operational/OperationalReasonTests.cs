using Diten.AuthService.Api.Operational;
using Diten.AuthService.Api.Services.ServiceIdentityTokens;

namespace Diten.AuthService.Application.Tests.Operational;

/// <summary>
/// WP-AUTH-BL570-01 FIX2 item 2 — the operator SEES why an operational command refused (the live Windows measurement
/// rests on these names). Only fixed codes reach stdout/stderr; a message that is not a code never does.
/// </summary>
[Collection(Diten.AuthService.Application.Tests.ServiceIdentityTokens.ConsoleCaptureCollection.Name)]
public sealed class OperationalReasonTests
{
    // FIX3 R3(b): the REAL entry point writes the named line to stderr and exits 2 — FailureLine alone being right is not
    // enough (a RunAsync that wrote the bare FailedClosed would leave every other test here green).
    [Fact]
    public async Task The_real_command_writes_the_named_refusal_to_stderr_and_exits_two()
    {
        var (out0, err0) = (Console.Out, Console.Error);
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        int exit;
        Console.SetOut(stdout);
        Console.SetError(stderr);
        try { exit = await EntitlementReconciliationOperationalMode.RunAsync(["--not-the-selector"]); }
        finally
        {
            Console.SetOut(out0);
            Console.SetError(err0);
        }

        Assert.Equal(2, exit);
        Assert.Equal("ENTITLEMENT_OPERATION_FAILED_CLOSED reason=EXACT_SELECTOR_REQUIRED" + Environment.NewLine, stderr.ToString());
        Assert.Equal(string.Empty, stdout.ToString());
    }

    // FIX3: a code followed by a line break is not a code (the anchor is \z, not $ — $ lets a trailing \n through).
    // FIX3 (CT sabotage C1 survived: the code shape with IgnoreCase let a lower / mixed-case single word through): a
    // secret-like word with no spaces is NEVER a reason — not on the line FailureLine builds (the one RunAsync writes to
    // stderr, R3b above), not as a channel's refusal reason.
    [Theory]
    [InlineData("s3cretTokenValue")]
    [InlineData("abcDEF123_xyz")]
    [InlineData("plan_hash_mismatch")]
    [InlineData("Plan_Hash_Mismatch")]
    [InlineData("eyJhbGciOiJIUzI1NiJ9")]
    public void A_lower_or_mixed_case_secret_like_word_never_reaches_the_operator(string word)
    {
        var line = EntitlementReconciliationOperationalMode.FailureLine(new InvalidOperationException(word));
        Assert.Equal(EntitlementReconciliationOperationalMode.FailedClosed, line);
        Assert.DoesNotContain(word, line, StringComparison.OrdinalIgnoreCase);
        Assert.Null(OperationalReason.From(new SecretChannelRejectedException(word)));
    }

    [Theory]
    [InlineData("PLAN_HASH_MISMATCH\n")]
    [InlineData("PLAN_HASH_MISMATCH\r\n")]
    public void A_code_with_a_trailing_line_break_is_not_a_code(string message)
    {
        Assert.Null(OperationalReason.From(new InvalidOperationException(message)));
    }

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
    [InlineData(ServiceClientSecretOutputSink.Reasons.FifoNotOwned)]
    public void A_secret_channel_refusal_carries_its_reason(string reason)
    {
        Assert.Equal(reason, OperationalReason.From(new SecretChannelRejectedException(reason)));
    }
}
