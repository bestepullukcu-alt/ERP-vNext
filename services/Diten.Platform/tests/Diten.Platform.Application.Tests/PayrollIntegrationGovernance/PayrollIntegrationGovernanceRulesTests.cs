using System.Reflection;
using Diten.Platform.API.Controllers.Platform;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.PayrollIntegrationGovernance;
using Diten.Platform.Application.Features.PayrollIntegrationGovernance.Commands;
using Diten.Platform.Application.Features.PayrollIntegrationGovernance.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.PayrollIntegrationGovernance.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.PayrollIntegrationGovernance.Queries;
using Diten.Platform.Application.Features.PayrollIntegrationGovernance.Validators;
using Diten.Platform.Application.Tests.HrisSources;
using Diten.Platform.Application.Tests.PayrollSources;
using Diten.Platform.Application.Tests.TenantOrganization;
using Diten.Platform.Application.Tests.TimeAttendanceProviders;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.HrisSources;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Entities.PayrollIntegrationGovernance;
using Diten.Platform.Domain.Entities.PayrollSources;
using Diten.Platform.Domain.Entities.TimeAttendanceProviders;
using Diten.Platform.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace Diten.Platform.Application.Tests.PayrollIntegrationGovernance;

public sealed class PayrollIntegrationGovernanceRulesTests
{
    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherTenantId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    [Fact]
    public async Task Create_sets_tenant_server_side_and_request_does_not_expose_tenant_id()
    {
        var context = Fixture();
        var handler = CreateRunHandler(context);

        var response = await handler.Handle(new CreatePayrollIntegrationRunCommand(RunRequest(context.PayrollSource.Id)), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.DoesNotContain(typeof(PayrollIntegrationRunRequest).GetProperties(), property => property.Name == "TenantId");
        var run = Assert.Single(context.Repository.Runs);
        Assert.Equal(TenantId, run.TenantId);
        Assert.Equal("PAYRUN-1", run.RunCode);
    }

    [Fact]
    public async Task Create_rejects_duplicate_run_code_and_idempotency_key()
    {
        var context = Fixture();
        context.Repository.Add(Run("PAYRUN-1", TenantId, context.PayrollSource.Id, "idem-1"));
        var handler = CreateRunHandler(context);

        var duplicateCode = await handler.Handle(new CreatePayrollIntegrationRunCommand(RunRequest(context.PayrollSource.Id, code: "payrun-1", idempotencyKey: "idem-2")), CancellationToken.None);
        var duplicateKey = await handler.Handle(new CreatePayrollIntegrationRunCommand(RunRequest(context.PayrollSource.Id, code: "payrun-2", idempotencyKey: "idem-1")), CancellationToken.None);

        Assert.Equal(409, duplicateCode.StatusCode);
        Assert.Equal(409, duplicateKey.StatusCode);
    }

    [Fact]
    public async Task Get_and_update_return_404_for_cross_tenant_run()
    {
        var context = Fixture();
        var otherRun = Run("OTHER", OtherTenantId, context.PayrollSource.Id, "other-idem");
        context.Repository.Add(otherRun);

        var getResponse = await new GetPayrollIntegrationRunByIdHandler(context.Repository).Handle(new GetPayrollIntegrationRunByIdQuery(otherRun.Id), CancellationToken.None);
        var updateResponse = await new UpdatePayrollIntegrationRunStatusHandler(context.Repository).Handle(new UpdatePayrollIntegrationRunStatusCommand(otherRun.Id, new PayrollIntegrationRunStatusRequest(PayrollIntegrationRunStatus.Completed, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "done")), CancellationToken.None);

        Assert.Equal(404, getResponse.StatusCode);
        Assert.Equal(404, updateResponse.StatusCode);
    }

    [Fact]
    public async Task Archive_soft_deletes_run_and_sets_deleted_at()
    {
        var context = Fixture();
        var run = Run("PAYRUN-1", TenantId, context.PayrollSource.Id, "idem-1");
        context.Repository.Add(run);

        var response = await new ArchivePayrollIntegrationRunHandler(context.Repository).Handle(new ArchivePayrollIntegrationRunCommand(run.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.True(run.IsDeleted);
        Assert.NotNull(run.DeletedAt);
    }

    [Fact]
    public void Validators_reject_raw_payload_secret_bank_tax_payslip_biometric_geolocation_and_calculation_markers()
    {
        var createValidator = new CreatePayrollIntegrationRunValidator();
        var mappingValidator = new UpdatePayrollIntegrationMappingControlValidator();

        Assert.False(createValidator.Validate(new CreatePayrollIntegrationRunCommand(RunRequest(Guid.NewGuid(), summary: "{\"payroll\":{\"gross\":1000}}"))).IsValid);
        Assert.False(createValidator.Validate(new CreatePayrollIntegrationRunCommand(RunRequest(Guid.NewGuid(), correlationId: "Bearer eyJhbGciOi.secret.token"))).IsValid);
        Assert.False(createValidator.Validate(new CreatePayrollIntegrationRunCommand(RunRequest(Guid.NewGuid(), summary: "bank tax payslip fingerprint latitude"))).IsValid);
        Assert.False(mappingValidator.Validate(new UpdatePayrollIntegrationMappingControlCommand(Guid.NewGuid(), Guid.NewGuid(), MappingRequest(PayrollIntegrationMappingScope.PayrollCalculation))).IsValid);
    }

    [Fact]
    public async Task Create_run_reference_validation_is_fail_closed()
    {
        var context = Fixture();
        var handler = CreateRunHandler(context);

        var response = await handler.Handle(new CreatePayrollIntegrationRunCommand(RunRequest(Guid.NewGuid())), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
        Assert.Empty(context.Repository.Runs);
    }

    [Fact]
    public async Task Source_link_rejects_cross_tenant_reference_and_accepts_deferred_state()
    {
        var context = Fixture();
        var run = Run("PAYRUN-1", TenantId, context.PayrollSource.Id, "idem-1");
        context.Repository.Add(run);
        var otherTenantPayrollSource = PayrollSource("OTHER-PAY", OtherTenantId);
        context.PayrollSources.Add(otherTenantPayrollSource);
        var handler = SourceLinkHandler(context);

        var validatedResponse = await handler.Handle(new RecordPayrollIntegrationSourceLinkCommand(run.Id, new PayrollIntegrationSourceLinkRequest(PayrollIntegrationSourceType.PayrollSource, otherTenantPayrollSource.Id, "1.0.0", PayrollIntegrationLinkState.Validated, null)), CancellationToken.None);
        var deferredResponse = await handler.Handle(new RecordPayrollIntegrationSourceLinkCommand(run.Id, new PayrollIntegrationSourceLinkRequest(PayrollIntegrationSourceType.PersonDirectory, Guid.NewGuid(), "1.0.0", PayrollIntegrationLinkState.Deferred, "deferred")), CancellationToken.None);

        Assert.Equal(404, validatedResponse.StatusCode);
        Assert.True(deferredResponse.IsSuccessful);
        Assert.Single(context.Repository.SourceLinks);
    }

    [Fact]
    public async Task Blind_reference_cannot_be_marked_governed_approved_or_mapped()
    {
        var context = Fixture();
        var run = Run("PAYRUN-1", TenantId, context.PayrollSource.Id, "idem-1");
        context.Repository.Add(run);
        var handler = new UpdatePayrollIntegrationMappingControlHandler(context.Repository);

        var response = await handler.Handle(new UpdatePayrollIntegrationMappingControlCommand(run.Id, Guid.NewGuid(), MappingRequest(PayrollIntegrationMappingScope.Employee, PayrollIntegrationControlState.Approved)), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
    }

    [Fact]
    public async Task Stale_contract_version_fails_closed()
    {
        var context = Fixture();
        var handler = CreateRunHandler(context);

        var response = await handler.Handle(new CreatePayrollIntegrationRunCommand(RunRequest(context.PayrollSource.Id, contractVersion: "stale-0.9")), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
    }

    [Fact]
    public async Task Replay_duplicate_idempotency_returns_conflict()
    {
        var context = Fixture();
        var run = Run("PAYRUN-1", TenantId, context.PayrollSource.Id, "idem-1");
        context.Repository.Add(run);
        var handler = new RequestPayrollIntegrationReplayHandler(context.Repository);

        var first = await handler.Handle(new RequestPayrollIntegrationReplayCommand(run.Id, ReplayRequest("replay-1")), CancellationToken.None);
        var second = await handler.Handle(new RequestPayrollIntegrationReplayCommand(run.Id, ReplayRequest("replay-1")), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
    }

    [Fact]
    public async Task Evidence_export_keeps_mod_0030_metadata_only()
    {
        var context = Fixture();
        var run = Run("PAYRUN-1", TenantId, context.PayrollSource.Id, "idem-1");
        context.Repository.Add(run);
        var recordsReferenceId = Guid.NewGuid();
        var handler = new RecordPayrollIntegrationEvidenceExportReferenceHandler(context.Repository);

        var response = await handler.Handle(new RecordPayrollIntegrationEvidenceExportReferenceCommand(run.Id, new PayrollIntegrationEvidenceExportReferenceRequest("AUDIT", Guid.NewGuid(), Guid.NewGuid(), recordsReferenceId, PayrollIntegrationEvidenceExportState.Requested, Guid.NewGuid(), "corr-1", "metadata only")), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        var reference = Assert.Single(context.Repository.EvidenceExports);
        Assert.Equal(recordsReferenceId, reference.RecordsRetentionReferenceId);
        Assert.Equal("AUDIT", reference.ExportPurposeCode);
    }

    [Fact]
    public void Controller_uses_platform_actor_policy_and_hardened_permissions()
    {
        var controllerType = typeof(PayrollIntegrationGovernanceController);
        var authorize = Assert.Single(controllerType.GetCustomAttributes<AuthorizeAttribute>());
        Assert.Equal("PlatformActor", authorize.Policy);

        AssertHasPermission(nameof(PayrollIntegrationGovernanceController.GetRuns), "platform.payroll-integration-governance.read");
        AssertHasPermission(nameof(PayrollIntegrationGovernanceController.CreateRun), "platform.payroll-integration-governance.create-run");
        AssertHasPermission(nameof(PayrollIntegrationGovernanceController.UpdateRunStatus), "platform.payroll-integration-governance.update-run-status");
        AssertHasPermission(nameof(PayrollIntegrationGovernanceController.RecordSourceLink), "platform.payroll-integration-governance.link-source");
        AssertHasPermission(nameof(PayrollIntegrationGovernanceController.UpdateMappingControl), "platform.payroll-integration-governance.manage-mapping");
        AssertHasPermission(nameof(PayrollIntegrationGovernanceController.RecordReconciliationControl), "platform.payroll-integration-governance.record-reconciliation");
        AssertHasPermission(nameof(PayrollIntegrationGovernanceController.CreateException), "platform.payroll-integration-governance.create-exception");
        AssertHasPermission(nameof(PayrollIntegrationGovernanceController.ResolveException), "platform.payroll-integration-governance.resolve-exception");
        AssertHasPermission(nameof(PayrollIntegrationGovernanceController.RequestReplay), "platform.payroll-integration-governance.request-replay");
        AssertHasPermission(nameof(PayrollIntegrationGovernanceController.RecordEvidenceExportReference), "platform.payroll-integration-governance.record-evidence-export");
        AssertHasPermission(nameof(PayrollIntegrationGovernanceController.RecordHealthSnapshot), "platform.payroll-integration-governance.record-health");
        AssertHasPermission(nameof(PayrollIntegrationGovernanceController.GetHealth), "platform.payroll-integration-governance.read-health");
    }

    private static TestContext Fixture()
    {
        var payrollSource = PayrollSource("ADP-MAIN", TenantId);
        var timeAttendanceProvider = TimeAttendanceProvider("UKG-MAIN", TenantId);
        var hrisSource = HrisSource("HRIS-MAIN", TenantId);
        var payrollSources = new InMemoryPayrollSourceRepository(TenantId);
        var timeAttendanceProviders = new InMemoryTimeAttendanceProviderRepository(TenantId);
        var hrisSources = new InMemoryHrisSourceRepository(TenantId);
        payrollSources.Add(payrollSource);
        timeAttendanceProviders.Add(timeAttendanceProvider);
        hrisSources.Add(hrisSource);
        return new TestContext(new InMemoryPayrollIntegrationGovernanceRepository(TenantId), payrollSources, timeAttendanceProviders, hrisSources, payrollSource, timeAttendanceProvider, hrisSource);
    }

    private static CreatePayrollIntegrationRunHandler CreateRunHandler(TestContext context) =>
        new(context.Repository, context.PayrollSources, context.TimeAttendanceProviders, context.HrisSources, TenantContext(TenantId));

    private static RecordPayrollIntegrationSourceLinkHandler SourceLinkHandler(TestContext context) =>
        new(context.Repository, context.PayrollSources, context.TimeAttendanceProviders, context.HrisSources, new InMemoryOrganizationUnitRepository(TenantId), new InMemoryPositionRepository(TenantId));

    private static PayrollIntegrationRunRequest RunRequest(
        Guid payrollSourceId,
        string code = "PAYRUN-1",
        string idempotencyKey = "idem-1",
        string contractVersion = "1.0.0",
        string correlationId = "corr-1",
        string? summary = null) =>
        new(code, payrollSourceId, null, null, contractVersion, PayrollIntegrationRunType.Manual, PayrollIntegrationRunStatus.Draft, Guid.NewGuid(), correlationId, idempotencyKey, null, null, summary);

    private static PayrollIntegrationMappingControlRequest MappingRequest(PayrollIntegrationMappingScope scope, PayrollIntegrationControlState state = PayrollIntegrationControlState.Pending) =>
        new(scope, Guid.NewGuid(), null, state, null, null);

    private static PayrollIntegrationRetryReplayRequestModel ReplayRequest(string idempotencyKey) =>
        new(PayrollIntegrationReplayRequestType.Replay, Guid.NewGuid(), "AUDIT", idempotencyKey, null, PayrollIntegrationReplayRequestState.Requested, "replay requested");

    private static PayrollIntegrationRun Run(string code, Guid tenantId, Guid payrollSourceId, string idempotencyKey) =>
        new()
        {
            TenantId = tenantId,
            RunCode = code,
            PayrollSourceProfileId = payrollSourceId,
            ContractVersion = "1.0.0",
            RunType = PayrollIntegrationRunType.Manual,
            Status = PayrollIntegrationRunStatus.Draft,
            CorrelationId = "corr-" + code,
            IdempotencyKey = idempotencyKey
        };

    private static PayrollExternalSystemProfile PayrollSource(string code, Guid tenantId) =>
        new()
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = code,
            ProviderFamily = PayrollProviderFamily.ADP,
            ExternalPayrollSystemId = "external-" + code,
            LifecycleState = PayrollSourceLifecycleState.Active
        };

    private static TimeAttendanceExternalProviderProfile TimeAttendanceProvider(string code, Guid tenantId) =>
        new()
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = code,
            ProviderFamily = TimeAttendanceProviderFamily.UKG,
            ExternalProviderAccountId = "external-" + code,
            LifecycleState = TimeAttendanceProviderLifecycleState.Active
        };

    private static HrisSourceProfile HrisSource(string code, Guid tenantId) =>
        new()
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = code,
            ProviderKind = HrisProviderKind.Workday,
            ConnectionProfileReference = "vault://hris/" + code.ToLowerInvariant(),
            LifecycleState = HrisSourceLifecycleState.Active
        };

    private static ITenantContext TenantContext(Guid tenantId)
    {
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(tenantId);
        return tenantContext;
    }

    private static void AssertHasPermission(string methodName, string expectedPermission)
    {
        var method = typeof(PayrollIntegrationGovernanceController).GetMethods().Single(x => x.Name == methodName);
        var attribute = Assert.Single(method.GetCustomAttributes<HasPermissionAttribute>());
        var permissionField = typeof(HasPermissionAttribute).GetField("_permission", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.Equal(expectedPermission, permissionField?.GetValue(attribute));
    }

    private sealed record TestContext(
        InMemoryPayrollIntegrationGovernanceRepository Repository,
        InMemoryPayrollSourceRepository PayrollSources,
        InMemoryTimeAttendanceProviderRepository TimeAttendanceProviders,
        InMemoryHrisSourceRepository HrisSources,
        PayrollExternalSystemProfile PayrollSource,
        TimeAttendanceExternalProviderProfile TimeAttendanceProvider,
        HrisSourceProfile HrisSource);
}
