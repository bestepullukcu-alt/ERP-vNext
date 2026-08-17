using System.Reflection;
using System.Security.Claims;
using Diten.HumanCapitalService.Api.Controllers.Hcm;
using Diten.HumanCapitalService.Api.Security;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.SensitiveAccess;
using Diten.HumanCapitalService.Application.Features.SensitiveAccess.Commands;
using Diten.HumanCapitalService.Application.Features.SensitiveAccess.Handlers;
using Diten.HumanCapitalService.Application.Features.SensitiveAccess.Queries;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;
using Diten.HumanCapitalService.Domain.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace Diten.HumanCapitalService.Application.Tests;

public sealed class SensitiveAccessTests
{
    [Fact]
    public async Task Standard_hr_requires_employee_projection_read_and_data_scope()
    {
        var projection = EmployeeProjection(EmployeeVisibilityClassification.StandardHr);
        var handler = Handler(new InMemoryEmployeeProjectionRepository(projection), DataScopeEvaluator.Allowed(), projection.TenantId);

        var response = await handler.Handle(Query(projection.Id, SensitiveAccessGuard.EmployeeProjectionReadPermission), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.NotNull(response.Data);
        Assert.True(response.Data!.IsAllowed);
        Assert.Equal(SensitiveAccessDecision.Allowed, response.Data.Decision);
        Assert.Equal("AccessAllowed", response.Data.ReasonCode);
    }

    [Fact]
    public async Task Sensitive_hr_requires_sensitive_access_read()
    {
        var projection = EmployeeProjection(EmployeeVisibilityClassification.SensitiveHr);
        var handler = Handler(new InMemoryEmployeeProjectionRepository(projection), DataScopeEvaluator.Allowed(), projection.TenantId);

        var response = await handler.Handle(Query(projection.Id, SensitiveAccessGuard.EmployeeProjectionReadPermission), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.NotNull(response.Data);
        Assert.False(response.Data!.IsAllowed);
        Assert.Equal(SensitiveAccessDecision.Denied, response.Data.Decision);
        Assert.Equal("MissingSensitiveAccessReadPermission", response.Data.ReasonCode);
        Assert.False(response.Data.DataScopeEvaluated);
    }

    [Fact]
    public async Task Restricted_hr_requires_review_and_audit_read()
    {
        var projection = EmployeeProjection(EmployeeVisibilityClassification.RestrictedHr);
        var handler = Handler(new InMemoryEmployeeProjectionRepository(projection), DataScopeEvaluator.Allowed(), projection.TenantId);

        var response = await handler.Handle(Query(
            projection.Id,
            SensitiveAccessGuard.EmployeeProjectionReadPermission,
            SensitiveAccessGuard.ReadPermission), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.NotNull(response.Data);
        Assert.False(response.Data!.IsAllowed);
        Assert.Equal(SensitiveAccessDecision.Denied, response.Data.Decision);
        Assert.Equal("MissingRestrictedHrPermissionSet", response.Data.ReasonCode);
    }

    [Fact]
    public async Task Restricted_hr_allows_with_review_audit_read_and_data_scope()
    {
        var projection = EmployeeProjection(EmployeeVisibilityClassification.RestrictedHr);
        var handler = Handler(new InMemoryEmployeeProjectionRepository(projection), DataScopeEvaluator.Allowed(), projection.TenantId);

        var response = await handler.Handle(Query(
            projection.Id,
            SensitiveAccessGuard.EmployeeProjectionReadPermission,
            SensitiveAccessGuard.ReviewPermission,
            SensitiveAccessGuard.AuditReadPermission), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.NotNull(response.Data);
        Assert.True(response.Data!.IsAllowed);
        Assert.Equal(SensitiveAccessDecision.Allowed, response.Data.Decision);
    }

    [Fact]
    public async Task Data_scope_unavailable_returns_explicit_deferred_state()
    {
        var projection = EmployeeProjection(EmployeeVisibilityClassification.StandardHr);
        var handler = Handler(new InMemoryEmployeeProjectionRepository(projection), DataScopeEvaluator.Unavailable(), projection.TenantId);

        var response = await handler.Handle(Query(projection.Id, SensitiveAccessGuard.EmployeeProjectionReadPermission), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.NotNull(response.Data);
        Assert.False(response.Data!.IsAllowed);
        Assert.Equal(SensitiveAccessDecision.Deferred, response.Data.Decision);
        Assert.Equal("DataScopeDeferred", response.Data.ReasonCode);
        Assert.Equal("DataScopeContractUnavailable", response.Data.DataScopeReasonCode);
    }

    [Fact]
    public async Task Data_scope_denied_fails_closed()
    {
        var projection = EmployeeProjection(EmployeeVisibilityClassification.StandardHr);
        var handler = Handler(new InMemoryEmployeeProjectionRepository(projection), DataScopeEvaluator.Denied(), projection.TenantId);

        var response = await handler.Handle(Query(projection.Id, SensitiveAccessGuard.EmployeeProjectionReadPermission), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.NotNull(response.Data);
        Assert.False(response.Data!.IsAllowed);
        Assert.Equal(SensitiveAccessDecision.Denied, response.Data.Decision);
        Assert.Equal("DataScopeDenied", response.Data.ReasonCode);
    }

    [Fact]
    public async Task Cross_tenant_projection_lookup_returns_not_found()
    {
        var projection = EmployeeProjection(EmployeeVisibilityClassification.StandardHr);
        var handler = Handler(new InMemoryEmployeeProjectionRepository(projection), DataScopeEvaluator.Allowed(), Guid.NewGuid());

        var response = await handler.Handle(Query(projection.Id, SensitiveAccessGuard.EmployeeProjectionReadPermission), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Sensitive_policy_validation_rejects_raw_secret_marker()
    {
        var handler = new ValidateSensitiveAccessPolicyHandler();

        var response = await handler.Handle(
            new ValidateSensitiveAccessPolicyCommand(new SensitiveAccessPolicyValidationRequest("v1-access_token")),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public async Task Audit_status_reports_local_bounded_deferred_mode()
    {
        var handler = new GetSensitiveAccessAuditStatusHandler();

        var response = await handler.Handle(new GetSensitiveAccessAuditStatusQuery(), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.NotNull(response.Data);
        Assert.Equal("LocalBoundedDeferred", response.Data!.AuditMode);
    }

    [Fact]
    public void Controller_entry_permissions_allow_handler_policy_matrix_to_execute()
    {
        Assert.Equal(SensitiveAccessGuard.ReadPermission, PermissionFor(nameof(SensitiveAccessController.GetHealth)));
        Assert.Equal(SensitiveAccessGuard.ReadPermission, PermissionFor(nameof(SensitiveAccessController.EvaluateEmployeeProjection)));
        Assert.Equal(SensitiveAccessGuard.ManagePermission, PermissionFor(nameof(SensitiveAccessController.ValidatePolicy)));
        Assert.Equal(SensitiveAccessGuard.AuditReadPermission, PermissionFor(nameof(SensitiveAccessController.GetDeferredAuditStatus)));
    }

    [Theory]
    [InlineData("permissions", "hcm.employee-projections.read hcm.sensitive-access.read")]
    [InlineData("scope", "hcm.employee-projections.read,hcm.sensitive-access.read")]
    [InlineData("permission", "hcm.employee-projections.read, hcm.sensitive-access.read")]
    public async Task Has_permission_attribute_accepts_space_or_comma_delimited_permission_claims(
        string claimType,
        string claimValue)
    {
        var attribute = new HasPermissionAttribute(SensitiveAccessGuard.ReadPermission);
        var context = AuthorizationContext(new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(claimType, claimValue)],
            "test")));

        await attribute.OnAuthorizationAsync(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public async Task Has_permission_attribute_forbids_when_delimited_claim_lacks_permission()
    {
        var attribute = new HasPermissionAttribute(SensitiveAccessGuard.ReadPermission);
        var context = AuthorizationContext(new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("permissions", "hcm.employee-projections.read,hcm.sensitive-access.review")],
            "test")));

        await attribute.OnAuthorizationAsync(context);

        Assert.IsType<ForbidResult>(context.Result);
    }

    private static string PermissionFor(string methodName)
    {
        var method = typeof(SensitiveAccessController).GetMethod(methodName)!;
        var attribute = method.GetCustomAttribute<HasPermissionAttribute>()!;
        return (string)typeof(HasPermissionAttribute)
            .GetField("_permission", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(attribute)!;
    }

    private static AuthorizationFilterContext AuthorizationContext(ClaimsPrincipal user)
    {
        var httpContext = new DefaultHttpContext { User = user };
        return new AuthorizationFilterContext(
            new ActionContext(httpContext, new RouteData(), new ActionDescriptor()),
            []);
    }

    private static EvaluateEmployeeProjectionSensitiveAccessHandler Handler(
        IEmployeeProjectionRepository repository,
        ISensitiveAccessDataScopeEvaluator dataScopeEvaluator,
        Guid tenantId) =>
        new(repository, dataScopeEvaluator, new FixedTenantContext(tenantId));

    private static EvaluateEmployeeProjectionSensitiveAccessQuery Query(Guid id, params string[] permissions) =>
        new(id, new SensitiveAccessDecisionRequest { SourcePolicyVersion = "v1" }, permissions);

    private static EmployeeProfileProjection EmployeeProjection(EmployeeVisibilityClassification visibility) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Code = "EMP-001",
            DisplayName = "Employee Routing Label",
            HrisSourceProfileId = Guid.NewGuid(),
            PersonReferenceId = Guid.NewGuid(),
            ExternalEmployeeReference = "EXT-001",
            EmploymentRecordReferenceKey = "EMPLOYMENT-001",
            EmploymentStatusCode = "ACTIVE",
            WorkerTypeCode = "EMPLOYEE",
            SourceContractVersion = "v1",
            ProjectionState = EmployeeProjectionState.Validated,
            VisibilityClassification = visibility,
            ProjectionVersion = 1
        };

    private sealed class FixedTenantContext : ITenantContext
    {
        public FixedTenantContext(Guid tenantId) => TenantId = tenantId;
        public Guid? TenantId { get; }
    }

    private sealed class DataScopeEvaluator : ISensitiveAccessDataScopeEvaluator
    {
        private readonly SensitiveAccessDataScopeResult _result;

        private DataScopeEvaluator(SensitiveAccessDataScopeResult result) => _result = result;

        public static DataScopeEvaluator Allowed() => new(SensitiveAccessDataScopeResult.Allowed());
        public static DataScopeEvaluator Denied() => new(SensitiveAccessDataScopeResult.OutOfScope());
        public static DataScopeEvaluator Unavailable() => new(SensitiveAccessDataScopeResult.ContractUnavailable());

        public Task<SensitiveAccessDataScopeResult> EvaluateAsync(
            Guid tenantId,
            EmployeeProfileProjection projection,
            CancellationToken ct)
        {
            _ = tenantId;
            _ = projection;
            _ = ct;
            return Task.FromResult(_result);
        }
    }

    private sealed class InMemoryEmployeeProjectionRepository : IEmployeeProjectionRepository
    {
        private readonly Dictionary<Guid, EmployeeProfileProjection> _items;

        public InMemoryEmployeeProjectionRepository(params EmployeeProfileProjection[] items) =>
            _items = items.ToDictionary(item => item.Id);

        public Task<IReadOnlyList<EmployeeProfileProjection>> ListAsync(Guid tenantId, CancellationToken ct)
        {
            _ = ct;
            return Task.FromResult<IReadOnlyList<EmployeeProfileProjection>>(
                _items.Values.Where(x => x.TenantId == tenantId && !x.IsDeleted).ToList());
        }

        public Task<EmployeeProfileProjection?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
        {
            _ = ct;
            return Task.FromResult(
                _items.TryGetValue(id, out var item) && item.TenantId == tenantId && !item.IsDeleted
                    ? item
                    : null);
        }

        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct)
        {
            _ = tenantId;
            _ = code;
            _ = excludingId;
            _ = ct;
            return Task.FromResult(false);
        }

        public Task CreateAsync(EmployeeProfileProjection projection, CancellationToken ct)
        {
            _ = ct;
            _items[projection.Id] = projection;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(EmployeeProfileProjection projection, CancellationToken ct)
        {
            _ = ct;
            _items[projection.Id] = projection;
            return Task.CompletedTask;
        }
    }
}
