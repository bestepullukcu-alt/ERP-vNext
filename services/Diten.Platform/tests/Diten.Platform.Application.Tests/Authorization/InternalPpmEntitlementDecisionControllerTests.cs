using System.Text.Json;
using Diten.Platform.API.Controllers.Internal;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Contracts.Entitlements;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.Authorization;

public sealed class InternalPpmEntitlementDecisionControllerTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private const string DedicatedCredential = "ppm-dedicated-test-credential";
    private const string AuthServiceCredential = "auth-service-test-credential";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("wrong-credential")]
    [InlineData(AuthServiceCredential)]
    public async Task MissingWrongOrAuthServiceCredentialReturns401(string? credential)
    {
        var checker = new Mock<IEntitlementChecker>(MockBehavior.Strict);
        var controller = CreateController(checker, credential);

        var result = await controller.GetDecision(TenantId, CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
        checker.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task EmptyTenantReturns400BeforeEntitlementLookup()
    {
        var checker = new Mock<IEntitlementChecker>(MockBehavior.Strict);
        var controller = CreateController(checker, DedicatedCredential);

        var result = await controller.GetDecision(Guid.Empty, CancellationToken.None);

        Assert.IsType<BadRequestResult>(result);
        checker.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DisabledEndpointReturns503WithoutCredentialOrEntitlementLookup()
    {
        var checker = new Mock<IEntitlementChecker>(MockBehavior.Strict);
        var controller = CreateController(checker, providedCredential: null, enabled: false);

        var result = await controller.GetDecision(TenantId, CancellationToken.None);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, Assert.IsType<StatusCodeResult>(result).StatusCode);
        checker.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ActivePpmReturnsExactAllowedContract()
    {
        var resolvedAt = DateTimeOffset.Parse("2026-07-30T10:00:00+03:00");
        var expiresAt = DateTimeOffset.Parse("2026-08-30T10:00:00+03:00");
        var checker = CreateChecker(EntitlementCheckResult.Allowed(
            EntitlementKind.Module,
            "PPM",
            expiresAt,
            effectiveScopes: null,
            resolvedFrom: EntitlementResolutionSource.Override,
            resolvedAtUtc: resolvedAt));
        var controller = CreateController(checker, DedicatedCredential);

        var result = await controller.GetDecision(TenantId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<PpmEntitlementDecisionV1>(ok.Value);
        Assert.Equal(TenantId, body.TenantId);
        Assert.Equal("PPM", body.ModuleCode);
        Assert.True(body.IsAllowed);
        Assert.Equal(TimeSpan.Zero, body.ResolvedAtUtc.Offset);
        Assert.Equal(TimeSpan.Zero, body.ExpiresAtUtc!.Value.Offset);
        VerifyFixedPpmLookup(checker);
    }

    [Theory]
    [InlineData(EntitlementDenyReason.ModuleNotEntitled)]
    [InlineData(EntitlementDenyReason.EntitlementDisabled)]
    [InlineData(EntitlementDenyReason.EntitlementExpired)]
    public async Task AuthoritativeBusinessDenyReturns200False(EntitlementDenyReason reason)
    {
        var checker = CreateChecker(EntitlementCheckResult.Denied(EntitlementKind.Module, "PPM", reason));
        var controller = CreateController(checker, DedicatedCredential);

        var result = await controller.GetDecision(TenantId, CancellationToken.None);

        var body = Assert.IsType<PpmEntitlementDecisionV1>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.False(body.IsAllowed);
        Assert.Equal("PPM", body.ModuleCode);
        VerifyFixedPpmLookup(checker);
    }

    [Fact]
    public async Task IndeterminateDecisionReturns503()
    {
        var checker = CreateChecker(EntitlementCheckResult.Denied(
            EntitlementKind.Module,
            "PPM",
            EntitlementDenyReason.ModuleNotEntitled,
            isCacheable: false));
        var controller = CreateController(checker, DedicatedCredential);

        var result = await controller.GetDecision(TenantId, CancellationToken.None);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, Assert.IsType<StatusCodeResult>(result).StatusCode);
        VerifyFixedPpmLookup(checker);
    }

    [Fact]
    public async Task DependencyExceptionReturns503()
    {
        var checker = new Mock<IEntitlementChecker>(MockBehavior.Strict);
        checker
            .Setup(value => value.IsModuleEntitledAsync(TenantId, "PPM", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("dependency unavailable"));
        var controller = CreateController(checker, DedicatedCredential);

        var result = await controller.GetDecision(TenantId, CancellationToken.None);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, Assert.IsType<StatusCodeResult>(result).StatusCode);
        VerifyFixedPpmLookup(checker);
    }

    [Fact]
    public void MalformedTenantRouteFallbackReturns400ForAuthorizedCaller()
    {
        var checker = new Mock<IEntitlementChecker>(MockBehavior.Strict);
        var controller = CreateController(checker, DedicatedCredential);

        var result = controller.RejectMalformedTenant("not-a-guid");

        Assert.IsType<BadRequestResult>(result);
        checker.VerifyNoOtherCalls();
    }

    [Fact]
    public void ContractSerializesToCanonicalClientFixtureWithExactFields()
    {
        var contract = new PpmEntitlementDecisionV1(
            TenantId,
            "PPM",
            true,
            DateTimeOffset.Parse("2026-07-30T10:00:00Z"),
            null);

        var json = JsonSerializer.Serialize(contract, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal(
            """{"tenantId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","moduleCode":"PPM","isAllowed":true,"resolvedAtUtc":"2026-07-30T10:00:00+00:00","expiresAtUtc":null}""",
            json);
        using var document = JsonDocument.Parse(json);
        Assert.Equal(
            ["tenantId", "moduleCode", "isAllowed", "resolvedAtUtc", "expiresAtUtc"],
            document.RootElement.EnumerateObject().Select(property => property.Name));
    }

    private static InternalPpmEntitlementDecisionController CreateController(
        Mock<IEntitlementChecker> checker,
        string? providedCredential,
        bool enabled = true)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PpmEntitlementDecision:Enabled"] = enabled.ToString(),
                ["PpmEntitlementDecision:ServiceCredential"] = DedicatedCredential,
                ["AuthService:InternalApiKey"] = AuthServiceCredential
            })
            .Build();
        var context = new DefaultHttpContext();
        context.TraceIdentifier = "test-correlation";
        if (providedCredential is not null)
        {
            context.Request.Headers[InternalPpmEntitlementDecisionController.ServiceCredentialHeader] = providedCredential;
        }

        return new InternalPpmEntitlementDecisionController(
            checker.Object,
            configuration,
            NullLogger<InternalPpmEntitlementDecisionController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };
    }

    private static Mock<IEntitlementChecker> CreateChecker(EntitlementCheckResult result)
    {
        var checker = new Mock<IEntitlementChecker>(MockBehavior.Strict);
        checker
            .Setup(value => value.IsModuleEntitledAsync(TenantId, "PPM", It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        return checker;
    }

    private static void VerifyFixedPpmLookup(Mock<IEntitlementChecker> checker)
    {
        checker.Verify(
            value => value.IsModuleEntitledAsync(TenantId, "PPM", It.IsAny<CancellationToken>()),
            Times.Once);
        checker.VerifyNoOtherCalls();
    }
}
