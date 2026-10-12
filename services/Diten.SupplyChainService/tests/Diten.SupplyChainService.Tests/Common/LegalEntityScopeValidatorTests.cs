using System.Net;
using Microsoft.Extensions.Configuration;
using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Infrastructure.Common;
using Xunit;
namespace Diten.SupplyChainService.Tests.Common;

/// <summary>
/// R-2 (PR #134). No Mongo: the validator is pure HTTP, so these facts are provable on an unprovisioned machine.
/// Every outcome other than Valid refuses the request — that is the fail-closed property, asserted rather than
/// described.
/// </summary>
public sealed class LegalEntityScopeValidatorTests
{
    private static readonly Guid Tenant = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid LegalEntity = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private const string Authorization = "Bearer mocked-not-validated";
    private const string BaseUrl = "http://mdm.invalid/";

    private static IConfiguration Config(string? baseUrl = BaseUrl) =>
        new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { [MdmLegalEntityScopeValidator.BaseUrlKey] = baseUrl }).Build();

    private static Task<LegalEntityScopeOutcome> Run(
        HttpMessageHandler handler,
        IConfiguration? config = null,
        Guid? tenant = null,
        Guid? legalEntity = null,
        string authorization = Authorization)
    {
        var client = new HttpClient(handler);
        var validator = new MdmLegalEntityScopeValidator(client, config ?? Config());
        return validator.ValidateAsync(
            tenant ?? Tenant, legalEntity ?? LegalEntity, authorization, Guid.NewGuid(), default);
    }

    [Fact]
    public async Task Ok_IsValid_AndTheRequestIsOneGetCarryingTenantAndCallerToken()
    {
        var captured = new List<HttpRequestMessage>();
        var outcome = await Run(new StubHandler(request =>
        {
            captured.Add(request);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
        }));

        Assert.Equal(LegalEntityScopeOutcome.Valid, outcome);
        var request = Assert.Single(captured);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal($"/api/legal-entities/{LegalEntity:D}/lookup-validation", request.RequestUri!.AbsolutePath);
        Assert.Equal(Authorization, request.Headers.GetValues("Authorization").Single());
        Assert.Equal(Tenant.ToString("D"), request.Headers.GetValues("X-Tenant-Id").Single());
        Assert.True(request.Headers.Contains("X-Correlation-Id"));
        // The legal entity travels in the path. Sending it as a scope header too would invite the callee to trust a
        // value this very call exists to check.
        Assert.False(request.Headers.Contains("X-Legal-Entity-Id"));
    }

    // MDM composes TenantFilter into the lookup, so another tenant's id and an inactive one are the same 404 to us.
    [Fact]
    public async Task NotFound_IsNotReferenceable_ForForeignAndForInactiveAlike() =>
        Assert.Equal(
            LegalEntityScopeOutcome.NotReferenceable,
            await Run(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound))));

    // A 403 is the caller missing mdm.legal-entities.read. Reporting it as NotReferenceable would dress a permission
    // gap as a missing record (Q420's distinction), so it refuses as Unavailable instead.
    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task EveryOtherStatus_IsUnavailable_NeverValid(HttpStatusCode status) =>
        Assert.Equal(
            LegalEntityScopeOutcome.Unavailable,
            await Run(new StubHandler(_ => new HttpResponseMessage(status))));

    [Fact]
    public async Task TransportFailure_IsUnavailable() =>
        Assert.Equal(
            LegalEntityScopeOutcome.Unavailable,
            await Run(new ThrowingHandler(new HttpRequestException("mdm unreachable"))));

    [Fact]
    public async Task Timeout_IsUnavailable() =>
        Assert.Equal(
            LegalEntityScopeOutcome.Unavailable,
            await Run(new ThrowingHandler(new TaskCanceledException("timeout"))));

    // An unconfigured service must not accept a legal entity it never checked, and must not pretend it checked one.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-url")]
    [InlineData("ftp://mdm.invalid/")]
    public async Task MissingOrNonHttpBaseUrl_IsUnavailable_AndNoCallIsMade(string? baseUrl)
    {
        var calls = 0;
        var outcome = await Run(new StubHandler(_ => { calls++; return new HttpResponseMessage(HttpStatusCode.OK); }), Config(baseUrl));
        Assert.Equal(LegalEntityScopeOutcome.Unavailable, outcome);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task EmptyIdentifiersOrBlankToken_AreRejectedBeforeAnyCall()
    {
        var calls = 0;
        HttpMessageHandler Counting() => new StubHandler(_ => { calls++; return new HttpResponseMessage(HttpStatusCode.OK); });

        Assert.Equal(LegalEntityScopeOutcome.NotReferenceable, await Run(Counting(), tenant: Guid.Empty));
        Assert.Equal(LegalEntityScopeOutcome.NotReferenceable, await Run(Counting(), legalEntity: Guid.Empty));
        Assert.Equal(LegalEntityScopeOutcome.NotReferenceable, await Run(Counting(), authorization: "   "));
        Assert.Equal(0, calls);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(send(request));
    }

    private sealed class ThrowingHandler(Exception error) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromException<HttpResponseMessage>(error);
    }
}
