using System.Security.Claims;
using System.Text;
using Diten.Platform.API.Models.Workflow;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Common.Tenancy;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Diten.Platform.Application.Tests.Security;

public sealed class TrustedWorkflowConsumerSecurityTests
{
    private static readonly Guid TenantId = Guid.Parse("21000000-0000-0000-0000-000000000021");
    private static readonly Guid ClientId = Guid.Parse("22000000-0000-0000-0000-000000000022");
    private static readonly Guid UserId = Guid.Parse("23000000-0000-0000-0000-000000000023");

    [Fact]
    public void Parser_accepts_exact_start_and_orders_unique_candidates()
    {
        var parser = new TrustedWorkflowConsumerRequestParser();

        var parsed = parser.TryParseStart(Encoding.UTF8.GetBytes(StartJson()), out var request);

        Assert.True(parsed);
        Assert.NotNull(request);
        Assert.Equal(new[] { "principal-a", "principal-b" }, request.CandidatePrincipalIds);
        Assert.Equal(TimeSpan.Zero, request.DueAt!.Value.Offset);
    }

    [Theory]
    [InlineData("\"tenantId\":\"21000000-0000-0000-0000-000000000021\",")]
    [InlineData("\"actorId\":\"23000000-0000-0000-0000-000000000023\",")]
    [InlineData("\"objecttype\":\"GlobalProduct\",")]
    [InlineData("\"objectType\":\"GlobalProduct\",\"objectType\":\"GSKU\",")]
    public void Parser_rejects_authority_unknown_case_drift_and_duplicate_fields(string injected)
    {
        var json = StartJson().Replace("\"objectType\":", injected + "\"objectType\":", StringComparison.Ordinal);

        Assert.False(new TrustedWorkflowConsumerRequestParser().TryParseStart(
            Encoding.UTF8.GetBytes(json),
            out _));
    }

    [Fact]
    public async Task Start_dual_auth_binds_same_tenant_dispatches_inside_scope_and_restores_contexts()
    {
        var authentication = new RecordingAuthenticationService(new Dictionary<string, AuthenticateResult>
        {
            [TrustedServiceTokenValidationExtensions.WorkflowAuthenticationScheme] = Success(ServicePrincipal()),
            [TrustedServiceTokenValidationExtensions.WorkflowDelegatedUserAuthenticationScheme] = Success(UserPrincipal())
        });
        var context = ValidStartContext(authentication);
        var originalPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
        context.User = originalPrincipal;
        var tenantContext = new TenantContext();
        var executor = new TrustedWorkflowConsumerRequestExecutor(new(), tenantContext);
        var dispatchCount = 0;

        var result = await executor.ExecuteStartAsync(
            context,
            CancellationToken.None,
            (request, key, service, delegated, _) =>
            {
                dispatchCount++;
                Assert.True(tenantContext.IsResolved);
                Assert.Equal(TenantId, tenantContext.TenantId);
                Assert.Equal(
                    TrustedServiceTokenValidationExtensions.WorkflowDelegatedUserAuthenticationScheme,
                    context.User.Identity?.AuthenticationType);
                Assert.Equal("operation-1", key);
                Assert.Equal(ClientId, service.ClientId);
                Assert.Equal(UserId, delegated.UserId);
                Assert.Equal("GlobalProduct", request.ObjectType);
                return Task.FromResult<IActionResult>(new OkResult());
            },
            Failure);

        Assert.IsType<OkResult>(result);
        Assert.Equal(1, dispatchCount);
        Assert.False(tenantContext.IsResolved);
        Assert.Same(originalPrincipal, context.User);
        Assert.Equal(
            new[]
            {
                TrustedServiceTokenValidationExtensions.WorkflowAuthenticationScheme,
                TrustedServiceTokenValidationExtensions.WorkflowDelegatedUserAuthenticationScheme
            },
            authentication.Schemes);
    }

    [Fact]
    public async Task Start_rejects_tenant_mismatch_and_never_dispatches()
    {
        var authentication = new RecordingAuthenticationService(new Dictionary<string, AuthenticateResult>
        {
            [TrustedServiceTokenValidationExtensions.WorkflowAuthenticationScheme] = Success(ServicePrincipal()),
            [TrustedServiceTokenValidationExtensions.WorkflowDelegatedUserAuthenticationScheme] = Success(
                UserPrincipal(Guid.NewGuid()))
        });
        var executor = new TrustedWorkflowConsumerRequestExecutor(new(), new TenantContext());
        var dispatched = false;

        var result = await executor.ExecuteStartAsync(
            ValidStartContext(authentication),
            CancellationToken.None,
            (_, _, _, _, _) =>
            {
                dispatched = true;
                return Task.FromResult<IActionResult>(new OkResult());
            },
            Failure);

        Assert.Equal(403, Assert.IsType<ObjectResult>(result).StatusCode);
        Assert.False(dispatched);
    }

    [Fact]
    public async Task Start_rejects_delegated_user_without_workflow_start_permission()
    {
        var authentication = new RecordingAuthenticationService(new Dictionary<string, AuthenticateResult>
        {
            [TrustedServiceTokenValidationExtensions.WorkflowAuthenticationScheme] = Success(ServicePrincipal()),
            [TrustedServiceTokenValidationExtensions.WorkflowDelegatedUserAuthenticationScheme] = Success(
                UserPrincipal(includeStartPermission: false))
        });
        var executor = new TrustedWorkflowConsumerRequestExecutor(new(), new TenantContext());

        var result = await executor.ExecuteStartAsync(
            ValidStartContext(authentication),
            CancellationToken.None,
            NeverStart,
            Failure);

        var forbidden = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, forbidden.StatusCode);
        Assert.Equal("WORKFLOW_DELEGATED_USER_FORBIDDEN", forbidden.Value);
    }

    [Fact]
    public async Task Start_rejects_tenant_header_and_duplicate_delegated_header_before_authentication()
    {
        var authentication = new RecordingAuthenticationService(
            new Dictionary<string, AuthenticateResult>());
        var executor = new TrustedWorkflowConsumerRequestExecutor(new(), new TenantContext());
        var tenantHeader = ValidStartContext(authentication);
        tenantHeader.Request.Headers["X-Tenant-Id"] = TenantId.ToString("D");
        var duplicateDelegated = ValidStartContext(authentication);
        duplicateDelegated.Request.Headers[TrustedServiceTokenValidationExtensions.DelegatedAuthorizationHeader] =
            new StringValues(["Bearer one", "Bearer two"]);

        var first = await executor.ExecuteStartAsync(
            tenantHeader,
            CancellationToken.None,
            NeverStart,
            Failure);
        var second = await executor.ExecuteStartAsync(
            duplicateDelegated,
            CancellationToken.None,
            NeverStart,
            Failure);

        Assert.Equal(403, Assert.IsType<ObjectResult>(first).StatusCode);
        Assert.Equal(403, Assert.IsType<ObjectResult>(second).StatusCode);
        Assert.Empty(authentication.Schemes);
    }

    [Fact]
    public async Task Evidence_uses_service_tenant_only_and_rejects_delegated_header()
    {
        var authentication = new RecordingAuthenticationService(new Dictionary<string, AuthenticateResult>
        {
            [TrustedServiceTokenValidationExtensions.WorkflowAuthenticationScheme] = Success(ServicePrincipal())
        });
        var context = ValidEvidenceContext(authentication);
        var tenantContext = new TenantContext();
        var executor = new TrustedWorkflowConsumerRequestExecutor(new(), tenantContext);
        var expectedInstance = Guid.Parse("24000000-0000-0000-0000-000000000024");

        var accepted = await executor.ExecuteEvidenceAsync(
            context,
            CancellationToken.None,
            (request, identity, _) =>
            {
                Assert.Equal(expectedInstance, request.WorkflowInstanceId);
                Assert.Equal("GlobalProduct", request.ExpectedObjectType);
                Assert.Equal("26000000-0000-0000-0000-000000000026", request.ExpectedObjectId);
                Assert.Equal(TenantId, identity.TenantId);
                Assert.Equal(TenantId, tenantContext.TenantId);
                return Task.FromResult<IActionResult>(new OkResult());
            },
            Failure);
        var forbidden = ValidEvidenceContext(authentication);
        forbidden.Request.Headers[TrustedServiceTokenValidationExtensions.DelegatedAuthorizationHeader] = "Bearer user";
        var rejected = await executor.ExecuteEvidenceAsync(
            forbidden,
            CancellationToken.None,
            (_, _, _) => Task.FromResult<IActionResult>(new OkResult()),
            Failure);

        Assert.IsType<OkResult>(accepted);
        Assert.False(tenantContext.IsResolved);
        Assert.Equal(403, Assert.IsType<ObjectResult>(rejected).StatusCode);
    }

    [Fact]
    public async Task Start_result_is_service_only_header_idempotent_and_strictly_parsed()
    {
        var authentication = new RecordingAuthenticationService(new Dictionary<string, AuthenticateResult>
        {
            [TrustedServiceTokenValidationExtensions.WorkflowAuthenticationScheme] = Success(ServicePrincipal())
        });
        var context = Context(authentication);
        context.Request.Headers.Authorization = "Bearer service-token";
        context.Request.Headers["Idempotency-Key"] = " operation-1 ";
        SetBody(context,
            "{\"expectedObjectType\":\"GlobalProduct\"," +
            "\"expectedObjectId\":\"GP-0001\"," +
            $"\"expectedMakerSubjectId\":\"{UserId:D}\"}}");
        var tenantContext = new TenantContext();
        var executor = new TrustedWorkflowConsumerRequestExecutor(new(), tenantContext);

        var result = await executor.ExecuteStartResultAsync(
            context,
            CancellationToken.None,
            (request, key, service, _) =>
            {
                Assert.Equal("operation-1", key);
                Assert.Equal(UserId, request.ExpectedMakerSubjectId);
                Assert.Equal(ClientId, service.ClientId);
                Assert.Equal(TenantId, tenantContext.TenantId);
                return Task.FromResult<IActionResult>(new OkResult());
            },
            Failure);

        Assert.IsType<OkResult>(result);
        Assert.False(tenantContext.IsResolved);
        Assert.Single(authentication.Schemes);
    }

    [Fact]
    public async Task Start_result_rejects_delegated_header_and_unknown_body_field()
    {
        var authentication = new RecordingAuthenticationService(new Dictionary<string, AuthenticateResult>
        {
            [TrustedServiceTokenValidationExtensions.WorkflowAuthenticationScheme] = Success(ServicePrincipal())
        });
        var executor = new TrustedWorkflowConsumerRequestExecutor(new(), new TenantContext());
        var delegated = Context(authentication);
        delegated.Request.Headers.Authorization = "Bearer service-token";
        delegated.Request.Headers["Idempotency-Key"] = "operation-1";
        delegated.Request.Headers[TrustedServiceTokenValidationExtensions.DelegatedAuthorizationHeader] = "Bearer user";
        SetBody(delegated, "{}");

        var rejectedDelegated = await executor.ExecuteStartResultAsync(
            delegated, CancellationToken.None, NeverStartResult, Failure);

        var unknown = Context(authentication);
        unknown.Request.Headers.Authorization = "Bearer service-token";
        unknown.Request.Headers["Idempotency-Key"] = "operation-1";
        SetBody(unknown,
            "{\"expectedObjectType\":\"GlobalProduct\",\"expectedObjectId\":\"GP-0001\"," +
            $"\"expectedMakerSubjectId\":\"{UserId:D}\",\"tenantId\":\"{TenantId:D}\"}}");
        var rejectedUnknown = await executor.ExecuteStartResultAsync(
            unknown, CancellationToken.None, NeverStartResult, Failure);

        Assert.Equal(403, Assert.IsType<ObjectResult>(rejectedDelegated).StatusCode);
        Assert.Equal(400, Assert.IsType<ObjectResult>(rejectedUnknown).StatusCode);
    }

    [Fact]
    public void Start_result_parser_rejects_duplicate_fields()
    {
        var json =
            "{\"expectedObjectType\":\"GlobalProduct\",\"expectedObjectType\":\"GSKU\"," +
            "\"expectedObjectId\":\"GP-0001\"," +
            $"\"expectedMakerSubjectId\":\"{UserId:D}\"}}";

        Assert.False(new TrustedWorkflowConsumerRequestParser().TryParseStartResult(
            Encoding.UTF8.GetBytes(json), out _));
    }

    [Fact]
    public async Task Start_result_budget_timeout_maps_to_504_and_caller_cancellation_propagates()
    {
        var authentication = new RecordingAuthenticationService(new Dictionary<string, AuthenticateResult>
        {
            [TrustedServiceTokenValidationExtensions.WorkflowAuthenticationScheme] = Success(ServicePrincipal())
        });
        var executor = new TrustedWorkflowConsumerRequestExecutor(new(), new TenantContext());
        var timeoutContext = ValidStartResultContext(authentication);

        var timeout = await executor.ExecuteStartResultAsync(
            timeoutContext,
            CancellationToken.None,
            async (_, _, _, token) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(10), token);
                return new OkResult();
            },
            Failure);

        using var caller = new CancellationTokenSource();
        caller.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => executor.ExecuteStartResultAsync(
            ValidStartResultContext(authentication),
            caller.Token,
            NeverStartResult,
            Failure));
        Assert.Equal(504, Assert.IsType<ObjectResult>(timeout).StatusCode);
    }

    [Fact]
    public async Task Cancellation_dual_auth_binds_service_and_delegated_requester_without_generic_permission()
    {
        var authentication = new RecordingAuthenticationService(new Dictionary<string, AuthenticateResult>
        {
            [TrustedServiceTokenValidationExtensions.WorkflowAuthenticationScheme] = Success(ServicePrincipal()),
            [TrustedServiceTokenValidationExtensions.WorkflowDelegatedUserAuthenticationScheme] = Success(
                UserPrincipal(includeStartPermission: false))
        });
        var context = ValidCancellationContext(authentication);
        var tenantContext = new TenantContext();
        var executor = new TrustedWorkflowConsumerRequestExecutor(new(), tenantContext);

        var result = await executor.ExecuteCancellationAsync(
            context,
            CancellationToken.None,
            (request, key, service, delegated, _) =>
            {
                Assert.Equal("cancel-operation-1", key);
                Assert.Equal(ClientId, service.ClientId);
                Assert.Equal(UserId, delegated.UserId);
                Assert.Equal(TenantId, tenantContext.TenantId);
                Assert.Equal("GlobalProduct", request.ExpectedObjectType);
                return Task.FromResult<IActionResult>(new OkResult());
            },
            Failure);

        Assert.IsType<OkResult>(result);
        Assert.False(tenantContext.IsResolved);
    }

    [Fact]
    public async Task Cancellation_preflight_is_service_only_and_rejects_delegated_or_tenant_headers()
    {
        var authentication = new RecordingAuthenticationService(new Dictionary<string, AuthenticateResult>
        {
            [TrustedServiceTokenValidationExtensions.WorkflowAuthenticationScheme] = Success(ServicePrincipal())
        });
        var executor = new TrustedWorkflowConsumerRequestExecutor(new(), new TenantContext());
        var delegated = ValidCancellationPreflightContext(authentication);
        delegated.Request.Headers[TrustedServiceTokenValidationExtensions.DelegatedAuthorizationHeader] =
            "Bearer delegated-token";
        var tenantHeader = ValidCancellationPreflightContext(authentication);
        tenantHeader.Request.Headers["X-Tenant-Id"] = TenantId.ToString("D");

        var delegatedResult = await executor.ExecuteCancellationPreflightAsync(
            delegated,
            CancellationToken.None,
            (_, _, _) => Task.FromResult<IActionResult>(new OkResult()),
            Failure);
        var tenantResult = await executor.ExecuteCancellationPreflightAsync(
            tenantHeader,
            CancellationToken.None,
            (_, _, _) => Task.FromResult<IActionResult>(new OkResult()),
            Failure);

        Assert.Equal(403, Assert.IsType<ObjectResult>(delegatedResult).StatusCode);
        Assert.Equal(403, Assert.IsType<ObjectResult>(tenantResult).StatusCode);
    }

    [Fact]
    public async Task Caller_cancellation_propagates_without_timeout_mapping()
    {
        var executor = new TrustedWorkflowConsumerRequestExecutor(new(), new TenantContext());
        using var caller = new CancellationTokenSource();
        caller.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => executor.ExecuteStartAsync(
            new DefaultHttpContext(),
            caller.Token,
            NeverStart,
            Failure));
    }

    private static DefaultHttpContext ValidStartContext(IAuthenticationService authentication)
    {
        var context = Context(authentication);
        context.Request.Headers.Authorization = "Bearer service-token";
        context.Request.Headers[TrustedServiceTokenValidationExtensions.DelegatedAuthorizationHeader] =
            "Bearer delegated-token";
        context.Request.Headers["Idempotency-Key"] = " operation-1 ";
        SetBody(context, StartJson());
        return context;
    }

    private static DefaultHttpContext ValidEvidenceContext(IAuthenticationService authentication)
    {
        var context = Context(authentication);
        context.Request.Headers.Authorization = "Bearer service-token";
        SetBody(
            context,
            "{\"workflowInstanceId\":\"24000000-0000-0000-0000-000000000024\"," +
            "\"expectedObjectType\":\"GlobalProduct\"," +
            "\"expectedObjectId\":\"26000000-0000-0000-0000-000000000026\"}");
        return context;
    }

    private static DefaultHttpContext ValidStartResultContext(IAuthenticationService authentication)
    {
        var context = Context(authentication);
        context.Request.Headers.Authorization = "Bearer service-token";
        context.Request.Headers["Idempotency-Key"] = "operation-1";
        SetBody(context,
            "{\"expectedObjectType\":\"GlobalProduct\"," +
            "\"expectedObjectId\":\"GP-0001\"," +
            $"\"expectedMakerSubjectId\":\"{UserId:D}\"}}");
        return context;
    }

    private static DefaultHttpContext ValidCancellationContext(IAuthenticationService authentication)
    {
        var context = ValidCancellationPreflightContext(authentication);
        context.Request.Headers[TrustedServiceTokenValidationExtensions.DelegatedAuthorizationHeader] =
            "Bearer delegated-token";
        context.Request.Headers["Idempotency-Key"] = "cancel-operation-1";
        SetBody(context,
            "{\"workflowInstanceId\":\"24000000-0000-0000-0000-000000000024\"," +
            "\"approvalTaskId\":\"25000000-0000-0000-0000-000000000025\"," +
            "\"expectedObjectType\":\"GlobalProduct\",\"expectedObjectId\":\"GP-0001\"," +
            $"\"expectedMakerSubjectId\":\"{UserId:D}\"," +
            "\"expectedWorkflowInstanceVersion\":1,\"expectedApprovalTaskVersion\":1," +
            "\"reasonCode\":\"WITHDRAW\"}");
        return context;
    }

    private static DefaultHttpContext ValidCancellationPreflightContext(IAuthenticationService authentication)
    {
        var context = Context(authentication);
        context.Request.Headers.Authorization = "Bearer service-token";
        SetBody(context,
            "{\"workflowInstanceId\":\"24000000-0000-0000-0000-000000000024\"," +
            "\"approvalTaskId\":\"25000000-0000-0000-0000-000000000025\"," +
            "\"expectedObjectType\":\"GlobalProduct\",\"expectedObjectId\":\"GP-0001\"," +
            $"\"expectedMakerSubjectId\":\"{UserId:D}\"}}");
        return context;
    }

    private static DefaultHttpContext Context(IAuthenticationService authentication)
    {
        var services = new ServiceCollection()
            .AddSingleton(authentication)
            .AddSingleton(typeof(IAuthenticationService), authentication)
            .BuildServiceProvider();
        return new DefaultHttpContext { RequestServices = services };
    }

    private static void SetBody(DefaultHttpContext context, string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        context.Request.Body = new MemoryStream(bytes);
        context.Request.ContentLength = bytes.Length;
    }

    private static string StartJson() =>
        """
        {
          "templateId":"25000000-0000-0000-0000-000000000025",
          "objectType":"GlobalProduct",
          "objectId":"26000000-0000-0000-0000-000000000026",
          "objectRef":null,
          "candidatePrincipalIds":["principal-b","principal-a"],
          "reasonCode":"SUBMIT",
          "commentRequired":false,
          "evidenceRequired":true,
          "dueAt":"2030-01-01T00:00:00+00:00"
        }
        """;

    private static ClaimsPrincipal ServicePrincipal() => Principal(
    [
        new("sub", ClientId.ToString("D")),
        new("tenant_id", TenantId.ToString("D")),
        new("jti", Guid.NewGuid().ToString("D")),
        new("actor_type", "service"),
        new("service_name", TrustedServiceTokenValidationExtensions.RequiredServiceName),
        new("aud", TrustedServiceTokenValidationExtensions.WorkflowRequiredAudience)
    ], TrustedServiceTokenValidationExtensions.WorkflowAuthenticationScheme);

    private static ClaimsPrincipal UserPrincipal(
        Guid? tenantId = null,
        bool includeStartPermission = true)
    {
        var claims = new List<Claim>
        {
            new("sub", UserId.ToString("D")),
            new("tenant_id", (tenantId ?? TenantId).ToString("D")),
            new("actor_type", "tenant_user")
        };
        if (includeStartPermission)
        {
            claims.Add(new("permission", WorkflowPermissions.InstancesStart));
        }

        return Principal(
            claims,
            TrustedServiceTokenValidationExtensions.WorkflowDelegatedUserAuthenticationScheme);
    }

    private static ClaimsPrincipal Principal(IEnumerable<Claim> claims, string authenticationType) =>
        new(new ClaimsIdentity(claims, authenticationType));

    private static AuthenticateResult Success(ClaimsPrincipal principal) =>
        AuthenticateResult.Success(new AuthenticationTicket(principal, principal.Identity!.AuthenticationType!));

    private static IActionResult Failure(int status, string code) =>
        new ObjectResult(code) { StatusCode = status };

    private static Task<IActionResult> NeverStart(
        TrustedWorkflowStartTransportRequest request,
        string key,
        TrustedWorkflowConsumerServiceIdentity service,
        TrustedWorkflowDelegatedUserIdentity delegated,
        CancellationToken cancellationToken) =>
        throw new Xunit.Sdk.XunitException("Dispatch must not run.");

    private static Task<IActionResult> NeverStartResult(
        TrustedWorkflowStartResultTransportRequest request,
        string key,
        TrustedWorkflowConsumerServiceIdentity service,
        CancellationToken cancellationToken) =>
        throw new Xunit.Sdk.XunitException("Dispatch must not run.");

    private sealed class RecordingAuthenticationService(
        IReadOnlyDictionary<string, AuthenticateResult> results) : IAuthenticationService
    {
        public List<string> Schemes { get; } = [];

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
        {
            Schemes.Add(scheme ?? string.Empty);
            return Task.FromResult(scheme is not null && results.TryGetValue(scheme, out var result)
                ? result
                : AuthenticateResult.NoResult());
        }

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
            throw new NotSupportedException();

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
            throw new NotSupportedException();

        public Task SignInAsync(
            HttpContext context,
            string? scheme,
            ClaimsPrincipal principal,
            AuthenticationProperties? properties) => throw new NotSupportedException();

        public Task SignOutAsync(
            HttpContext context,
            string? scheme,
            AuthenticationProperties? properties) => throw new NotSupportedException();
    }
}
