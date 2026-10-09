using System.Text;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Common.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.Audit;

/// <summary>
/// CT acceptance of the product-five takeover (2026-10-01). <c>InternalTrustedSourceAuditIntentController.Accept</c> is
/// <c>[AllowAnonymous]</c>: <see cref="TrustedSourceAuditIntentRequestExecutor"/> is the whole gate. The existing
/// executor tests covered the header shape, the unavailable validator and the tenant mismatch, and mocked the service
/// identity as authenticated everywhere else — the two refusals that matter most (the token is NOT a valid service
/// token; it is valid but NOT authorized) had no test. Measured by sabotage: with both removed, nothing went red.
///
/// <para>Each refusal is exercised with the identity saying NO, and each asserts that nothing behind it ran: the body
/// is never read, the acceptance service is never called, no tenant scope is opened.</para>
/// </summary>
public sealed class TrustedSourceAuditIntentExecutorRefusalTests
{
    [Fact]
    public async Task A_token_that_is_not_a_valid_service_token_is_401_and_nothing_behind_the_gate_runs()
    {
        var outcome = await ExecuteWith(ITrustedSourceAuditIntentServiceIdentity.IdentityResult.Unauthenticated);

        Assert.Equal((401, "AUDIT_SOURCE_INTENT_UNAUTHENTICATED"), outcome.Refusal);
        Assert.Equal(0, outcome.BodyReads);
        Assert.False(outcome.TenantResolved);
    }

    [Fact]
    public async Task A_valid_token_that_is_not_authorized_for_audit_ingest_is_403_and_nothing_behind_the_gate_runs()
    {
        var outcome = await ExecuteWith(ITrustedSourceAuditIntentServiceIdentity.IdentityResult.Forbidden);

        Assert.Equal((403, "AUDIT_SOURCE_INTENT_FORBIDDEN"), outcome.Refusal);
        Assert.Equal(0, outcome.BodyReads);
        Assert.False(outcome.TenantResolved);
    }

    /// <summary>Authenticated and authorized, but the token names no tenant: there is no tenant to write under.</summary>
    [Fact]
    public async Task An_authorized_service_token_with_no_tenant_is_403_and_nothing_behind_the_gate_runs()
    {
        var outcome = await ExecuteWith(new ITrustedSourceAuditIntentServiceIdentity.IdentityResult(true, true, false, null));

        Assert.Equal((403, "AUDIT_SOURCE_INTENT_FORBIDDEN"), outcome.Refusal);
        Assert.Equal(0, outcome.BodyReads);
        Assert.False(outcome.TenantResolved);
    }

    private sealed record Outcome((int Status, string Code)? Refusal, int BodyReads, bool TenantResolved);

    private static async Task<Outcome> ExecuteWith(ITrustedSourceAuditIntentServiceIdentity.IdentityResult answer)
    {
        var identity = new Mock<ITrustedSourceAuditIntentServiceIdentity>();
        identity.Setup(x => x.ResolveAsync(It.IsAny<HttpContext>())).ReturnsAsync(answer);
        // Strict: any call to the acceptance service fails the test by itself.
        var acceptance = new Mock<ITrustedSourceAuditIntentAcceptanceService>(MockBehavior.Strict);
        var tenantContext = new TenantContext();
        var body = new CountingStream(Encoding.UTF8.GetBytes("{}"));
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer token";
        context.Request.Body = body;
        context.Request.ContentLength = body.Length;

        (int, string)? refusal = null;
        await new TrustedSourceAuditIntentRequestExecutor(identity.Object, new(), acceptance.Object, tenantContext)
            .ExecuteAsync(
                context,
                default,
                accepted => new ObjectResult(accepted) { StatusCode = 201 },
                (status, code) =>
                {
                    refusal = (status, code);
                    return new ObjectResult(code) { StatusCode = status };
                });

        acceptance.VerifyNoOtherCalls();
        return new(refusal, body.Reads, tenantContext.IsResolved);
    }

    private sealed class CountingStream(byte[] bytes) : MemoryStream(bytes)
    {
        public int Reads { get; private set; }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Reads++;
            return base.ReadAsync(buffer, cancellationToken);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            Reads++;
            return base.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            Reads++;
            return base.Read(buffer, offset, count);
        }
    }
}
