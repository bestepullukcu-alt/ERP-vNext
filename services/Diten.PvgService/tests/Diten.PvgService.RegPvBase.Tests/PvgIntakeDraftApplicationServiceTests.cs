using Diten.PvgService.Application.RegPvBase;
using Diten.PvgService.Domain.RegPvBase;
using Diten.PvgService.Infrastructure.RegPvBase;
using Xunit;

namespace Diten.PvgService.RegPvBase.Tests;

public sealed class PvgIntakeDraftApplicationServiceTests
{
    private static readonly string[] SensitiveSamples =
    [
        "tenant-secret-123",
        "patient-subject-code",
        "reporter@example.test",
        "free text narrative with PHI",
        "triage free-text reason",
        "queue-safety-review",
        "updated-source-ref",
        "suspect product update",
        "foreign-tenant-secret",
        "raw exception message",
        "actor-secret-456",
        "corr-secret-789"
    ];

    [Fact]
    public async Task Create_validation_runs_before_ports_and_mutation()
    {
        var callLog = new List<string>();
        var fieldPolicy = new RecordingFieldSecurityPolicy(callLog);
        var workflowGate = new RecordingWorkflowTransitionGate(callLog);
        var evidencePort = new RecordingEvidenceLinkPort(callLog);
        var service = NewService(fieldPolicy, workflowGate, evidencePort, new RecordingPermissionGate(callLog));

        var invalid = await service.CreateDraftAsync(
            CreateCommand(InvalidCreateRequest()));

        Assert.False(invalid.Result.IsSuccess);
        Assert.Equal(PvgApplicationOutcome.Invalid, invalid.Result.Outcome);
        Assert.Null(invalid.IntakeDraftId);
        Assert.Null(invalid.AuditIntent);
        Assert.Empty(callLog);
        AssertSafe(invalid);
    }

    [Fact]
    public async Task Missing_actor_blocks_mutation_before_permission_ports_and_audit_intent()
    {
        var callLog = new List<string>();
        var service = NewService(
            new RecordingFieldSecurityPolicy(callLog),
            new RecordingWorkflowTransitionGate(callLog),
            new RecordingEvidenceLinkPort(callLog),
            new RecordingPermissionGate(callLog));

        var blocked = await service.CreateDraftAsync(
            new CreateIntakeDraftCommand(TenantContext(), null!, CorrelationContext(), ValidCreateRequest()));

        Assert.False(blocked.Result.IsSuccess);
        Assert.Equal(PvgPermissionReasonCodes.ActorContextRequired, blocked.Result.ReasonCode);
        Assert.Null(blocked.IntakeDraftId);
        Assert.Null(blocked.AuditIntent);
        Assert.Empty(callLog);
        AssertSafe(blocked);
    }

    [Fact]
    public async Task Missing_permission_blocks_mutation_before_ports_and_audit_intent()
    {
        var callLog = new List<string>();
        var permissionGate = new RecordingPermissionGate(callLog)
        {
            Decision = PvgPermissionDecision.Denied(PvgPermissionReasonCodes.PermissionDenied)
        };
        var service = NewService(
            new RecordingFieldSecurityPolicy(callLog),
            new RecordingWorkflowTransitionGate(callLog),
            new RecordingEvidenceLinkPort(callLog),
            permissionGate);

        var blocked = await service.CreateDraftAsync(CreateCommand(ValidCreateRequest()));

        Assert.False(blocked.Result.IsSuccess);
        Assert.Equal(PvgPermissionReasonCodes.PermissionDenied, blocked.Result.ReasonCode);
        Assert.Null(blocked.IntakeDraftId);
        Assert.Null(blocked.AuditIntent);
        Assert.Equal(["permission:Create:pvg.mod0230.intake.create"], callLog);
        AssertSafe(blocked);
    }

    [Fact]
    public async Task Invalid_correlation_blocks_mutation_before_permission_ports_and_audit_intent()
    {
        var callLog = new List<string>();
        var service = NewService(
            new RecordingFieldSecurityPolicy(callLog),
            new RecordingWorkflowTransitionGate(callLog),
            new RecordingEvidenceLinkPort(callLog),
            new RecordingPermissionGate(callLog));

        var blocked = await service.CreateDraftAsync(
            new CreateIntakeDraftCommand(TenantContext(), ActorContext(), new PvgCorrelationContext("corr secret 789"), ValidCreateRequest()));

        Assert.False(blocked.Result.IsSuccess);
        Assert.Equal(PvgPermissionReasonCodes.CorrelationContextInvalid, blocked.Result.ReasonCode);
        Assert.Null(blocked.IntakeDraftId);
        Assert.Null(blocked.AuditIntent);
        Assert.Empty(callLog);
        AssertSafe(blocked);
    }

    [Fact]
    public async Task Create_denied_by_field_security_blocks_without_sensitive_echo()
    {
        var callLog = new List<string>();
        var fieldPolicy = new RecordingFieldSecurityPolicy(callLog)
        {
            Decision = PvgPortDecision.FieldSecurityDenied()
        };
        var service = NewService(
            fieldPolicy,
            new RecordingWorkflowTransitionGate(callLog),
            new RecordingEvidenceLinkPort(callLog),
            new RecordingPermissionGate(callLog));

        var result = await service.CreateDraftAsync(
            CreateCommand(ValidCreateRequest()));

        Assert.False(result.Result.IsSuccess);
        Assert.Equal(PvgApplicationOutcome.Blocked, result.Result.Outcome);
        Assert.Equal(PvgSafeReasonCodes.FieldSecurityPolicyUnavailable, result.Result.ReasonCode);
        Assert.Null(result.IntakeDraftId);
        Assert.Null(result.AuditIntent);
        Assert.Equal("permission:Create:pvg.mod0230.intake.create", callLog[0]);
        Assert.StartsWith("field:", callLog[1], StringComparison.Ordinal);
        AssertSafe(result);
    }

    [Fact]
    public async Task Create_and_list_succeed_with_allowed_ports_and_return_safe_metadata_only()
    {
        var callLog = new List<string>();
        var service = NewService(
            new RecordingFieldSecurityPolicy(callLog),
            new RecordingWorkflowTransitionGate(callLog),
            new RecordingEvidenceLinkPort(callLog),
            new RecordingPermissionGate(callLog));

        var created = await service.CreateDraftAsync(
            CreateCommand(ValidCreateRequest()));

        Assert.True(created.Result.IsSuccess);
        Assert.NotNull(created.IntakeDraftId);
        Assert.NotNull(created.AuditIntent);
        Assert.Equal(PvgIntakeOperation.Create, created.AuditIntent.Operation);
        Assert.Equal("pvg.mod0230.intake.create", created.AuditIntent.RequiredPermission);
        Assert.Equal("HumanUser", created.AuditIntent.ActorKind);
        Assert.True(created.AuditIntent.HasCorrelation);
        Assert.Equal("permission:Create:pvg.mod0230.intake.create", callLog[0]);
        Assert.Contains(callLog, entry => entry.StartsWith("field:", StringComparison.Ordinal));
        Assert.Contains(callLog, entry => entry == "evidence:Create");

        var listed = await service.ListDraftsAsync(
            ReadListQuery());

        Assert.True(listed.Result.IsSuccess);
        Assert.Single(listed.Items);
        Assert.Equal(created.IntakeDraftId, listed.Items[0].IntakeDraftId);
        Assert.Equal(PvgIntakeStatus.IntakeCreated, listed.Items[0].Status);
        AssertSafe(created);
        AssertSafe(listed);
    }

    [Fact]
    public async Task Update_denied_by_field_security_keeps_existing_status()
    {
        var callLog = new List<string>();
        var fieldPolicy = new RecordingFieldSecurityPolicy(callLog);
        var service = NewService(
            fieldPolicy,
            new RecordingWorkflowTransitionGate(callLog),
            new RecordingEvidenceLinkPort(callLog),
            new RecordingPermissionGate(callLog));
        var created = await service.CreateDraftAsync(
            CreateCommand(ValidCreateRequest()));
        fieldPolicy.Decision = PvgPortDecision.FieldSecurityDenied();

        var updated = await service.UpdateDraftAsync(
            new UpdateIntakeDraftCommand(
                TenantContext(),
                ActorContext(),
                CorrelationContext(),
                created.IntakeDraftId!,
                ValidUpdateRequest()));

        Assert.False(updated.Result.IsSuccess);
        Assert.Equal(PvgSafeReasonCodes.FieldSecurityPolicyUnavailable, updated.Result.ReasonCode);
        Assert.Null(updated.AuditIntent);

        fieldPolicy.Decision = AllowedDecision();
        var fetched = await service.GetDraftByIdAsync(
            ReadByIdQuery(created.IntakeDraftId!));

        Assert.Single(fetched.Items);
        Assert.Equal(PvgIntakeStatus.IntakeCreated, fetched.Items[0].Status);
        AssertSafe(updated);
        AssertSafe(fetched);
    }

    [Fact]
    public async Task Triage_denied_by_workflow_gate_blocks_before_mutation()
    {
        var callLog = new List<string>();
        var workflowGate = new RecordingWorkflowTransitionGate(callLog);
        var service = NewService(
            new RecordingFieldSecurityPolicy(callLog),
            workflowGate,
            new RecordingEvidenceLinkPort(callLog),
            new RecordingPermissionGate(callLog));
        var created = await service.CreateDraftAsync(
            CreateCommand(ValidCreateRequest()));
        workflowGate.Decision = PvgPortDecision.WorkflowTransitionDenied();

        var triaged = await service.TriageDraftAsync(
            new TriageIntakeDraftCommand(
                TenantContext(),
                ActorContext(),
                CorrelationContext(),
                created.IntakeDraftId!,
                new PvgTriageIntakeDraftRequest(PvgTriageOutcome.Rejected, "PVG_TRIAGE_REASON_REJECTED", "triage free-text reason")));

        Assert.False(triaged.Result.IsSuccess);
        Assert.Equal(PvgSafeReasonCodes.WorkflowTransitionGateUnavailable, triaged.Result.ReasonCode);
        Assert.Null(triaged.AuditIntent);
        Assert.Contains("permission:Triage:pvg.mod0230.intake.triage", callLog);
        Assert.Contains(callLog, entry => entry.StartsWith("field:Triage:triage:", StringComparison.Ordinal));
        Assert.Contains("workflow:Triage", callLog);
        Assert.DoesNotContain("evidence:Triage", callLog);
        Assert.True(
            callLog.FindIndex(entry => entry.StartsWith("field:Triage:triage:", StringComparison.Ordinal)) <
            callLog.FindIndex(entry => entry == "workflow:Triage"));

        workflowGate.Decision = AllowedDecision();
        var fetched = await service.GetDraftByIdAsync(
            ReadByIdQuery(created.IntakeDraftId!));

        Assert.Single(fetched.Items);
        Assert.Equal(PvgIntakeStatus.IntakeCreated, fetched.Items[0].Status);
        AssertSafe(triaged);
    }

    [Fact]
    public async Task Route_denied_by_evidence_link_blocks_after_workflow_before_mutation()
    {
        var callLog = new List<string>();
        var evidencePort = new RecordingEvidenceLinkPort(callLog);
        var service = NewService(
            new RecordingFieldSecurityPolicy(callLog),
            new RecordingWorkflowTransitionGate(callLog),
            evidencePort,
            new RecordingPermissionGate(callLog));
        var created = await service.CreateDraftAsync(
            CreateCommand(ValidCreateRequest()));
        evidencePort.Decision = PvgPortDecision.EvidenceLinkDenied();

        var routed = await service.RouteDraftAsync(
            new RouteIntakeDraftCommand(
                TenantContext(),
                ActorContext(),
                CorrelationContext(),
                created.IntakeDraftId!,
                new PvgRouteIntakeDraftRequest("queue-safety-review")));

        Assert.False(routed.Result.IsSuccess);
        Assert.Equal(PvgSafeReasonCodes.EvidenceLinkUnavailable, routed.Result.ReasonCode);
        Assert.Null(routed.AuditIntent);
        Assert.Contains("permission:Route:pvg.mod0230.intake.route", callLog);
        Assert.Contains("workflow:Route", callLog);
        Assert.Contains("evidence:Route", callLog);

        evidencePort.Decision = AllowedDecision();
        var fetched = await service.GetDraftByIdAsync(
            ReadByIdQuery(created.IntakeDraftId!));

        Assert.Single(fetched.Items);
        Assert.Equal(PvgIntakeStatus.IntakeCreated, fetched.Items[0].Status);
        AssertSafe(routed);
    }

    [Fact]
    public async Task Queries_fail_closed_when_field_security_denies()
    {
        var callLog = new List<string>();
        var fieldPolicy = new RecordingFieldSecurityPolicy(callLog);
        var service = NewService(
            fieldPolicy,
            new RecordingWorkflowTransitionGate(callLog),
            new RecordingEvidenceLinkPort(callLog),
            new RecordingPermissionGate(callLog));
        var created = await service.CreateDraftAsync(
            CreateCommand(ValidCreateRequest()));
        fieldPolicy.Decision = PvgPortDecision.FieldSecurityDenied();

        var fetched = await service.GetDraftByIdAsync(
            ReadByIdQuery(created.IntakeDraftId!));
        var listed = await service.ListDraftsAsync(
            ReadListQuery());

        Assert.False(fetched.Result.IsSuccess);
        Assert.False(listed.Result.IsSuccess);
        Assert.Empty(fetched.Items);
        Assert.Empty(listed.Items);
        Assert.Equal(PvgSafeReasonCodes.FieldSecurityPolicyUnavailable, fetched.Result.ReasonCode);
        Assert.Equal(PvgSafeReasonCodes.FieldSecurityPolicyUnavailable, listed.Result.ReasonCode);
        AssertSafe(fetched);
        AssertSafe(listed);
    }

    [Theory]
    [InlineData(PvgIntakeOperation.Create, "create")]
    [InlineData(PvgIntakeOperation.Update, "update")]
    [InlineData(PvgIntakeOperation.Triage, "triage")]
    [InlineData(PvgIntakeOperation.Route, "route")]
    [InlineData(PvgIntakeOperation.GetById, "detail")]
    [InlineData(PvgIntakeOperation.GetList, "list")]
    public async Task Field_security_policy_unavailable_fails_closed_for_supported_MOD_0230_operations(
        PvgIntakeOperation operation,
        string expectedSurface)
    {
        var callLog = new List<string>();
        var fieldPolicy = new RecordingFieldSecurityPolicy(callLog);
        var service = NewService(
            fieldPolicy,
            new RecordingWorkflowTransitionGate(callLog),
            new RecordingEvidenceLinkPort(callLog),
            new RecordingPermissionGate(callLog));

        string? intakeDraftId = null;
        if (operation != PvgIntakeOperation.Create)
        {
            var created = await service.CreateDraftAsync(CreateCommand(ValidCreateRequest()));
            Assert.True(created.Result.IsSuccess);
            intakeDraftId = created.IntakeDraftId;
            callLog.Clear();
        }

        fieldPolicy.Decision = PvgPortDecision.FieldSecurityDenied();

        var blocked = operation switch
        {
            PvgIntakeOperation.Create => (object)await service.CreateDraftAsync(CreateCommand(ValidCreateRequest())),
            PvgIntakeOperation.Update => (object)await service.UpdateDraftAsync(
                new UpdateIntakeDraftCommand(
                    TenantContext(),
                    ActorContext(),
                    CorrelationContext(),
                    intakeDraftId!,
                    ValidUpdateRequest())),
            PvgIntakeOperation.Triage => (object)await service.TriageDraftAsync(
                new TriageIntakeDraftCommand(
                    TenantContext(),
                    ActorContext(),
                    CorrelationContext(),
                    intakeDraftId!,
                    new PvgTriageIntakeDraftRequest(PvgTriageOutcome.Rejected, "PVG_TRIAGE_REASON_REJECTED", "triage free-text reason"))),
            PvgIntakeOperation.Route => (object)await service.RouteDraftAsync(
                new RouteIntakeDraftCommand(
                    TenantContext(),
                    ActorContext(),
                    CorrelationContext(),
                    intakeDraftId!,
                    new PvgRouteIntakeDraftRequest("queue-safety-review"))),
            PvgIntakeOperation.GetById => (object)await service.GetDraftByIdAsync(ReadByIdQuery(intakeDraftId!)),
            PvgIntakeOperation.GetList => (object)await service.ListDraftsAsync(ReadListQuery()),
            _ => throw new InvalidOperationException($"Unsupported FieldSecurity test operation {operation}.")
        };

        Assert.Contains(callLog, entry => entry.StartsWith($"field:{operation}:{expectedSurface}:", StringComparison.Ordinal));
        AssertFieldSecurityBlocked(blocked);
        AssertSafe(blocked);
    }

    [Fact]
    public async Task Triage_allowed_by_field_security_calls_workflow_evidence_and_mutates_safely()
    {
        var callLog = new List<string>();
        var fieldPolicy = new RecordingFieldSecurityPolicy(callLog);
        var service = NewService(
            fieldPolicy,
            new RecordingWorkflowTransitionGate(callLog),
            new RecordingEvidenceLinkPort(callLog),
            new RecordingPermissionGate(callLog));
        var created = await service.CreateDraftAsync(CreateCommand(ValidCreateRequest()));
        Assert.True(created.Result.IsSuccess);
        callLog.Clear();

        var triaged = await service.TriageDraftAsync(
            new TriageIntakeDraftCommand(
                TenantContext(),
                ActorContext(),
                CorrelationContext(),
                created.IntakeDraftId!,
                new PvgTriageIntakeDraftRequest(PvgTriageOutcome.Rejected, "PVG_TRIAGE_REASON_REJECTED", "triage free-text reason")));

        Assert.True(triaged.Result.IsSuccess);
        Assert.Contains("field:Triage:triage:TriageOutcome", callLog);
        Assert.Contains("field:Triage:triage:TriageReason", callLog);
        Assert.Contains("workflow:Triage", callLog);
        Assert.Contains("evidence:Triage", callLog);
        Assert.True(
            callLog.IndexOf("field:Triage:triage:TriageOutcome") <
            callLog.FindIndex(entry => entry == "workflow:Triage"));

        var fetched = await service.GetDraftByIdAsync(
            ReadByIdQuery(created.IntakeDraftId!));

        Assert.Single(fetched.Items);
        Assert.Equal(PvgIntakeStatus.Triaged, fetched.Items[0].Status);
        AssertSafe(triaged);
        AssertSafe(fetched);
    }

    [Fact]
    public async Task Triage_denied_by_field_security_blocks_before_workflow_evidence_and_mutation()
    {
        var callLog = new List<string>();
        var fieldPolicy = new RecordingFieldSecurityPolicy(callLog);
        var service = NewService(
            fieldPolicy,
            new RecordingWorkflowTransitionGate(callLog),
            new RecordingEvidenceLinkPort(callLog),
            new RecordingPermissionGate(callLog));
        var created = await service.CreateDraftAsync(CreateCommand(ValidCreateRequest()));
        Assert.True(created.Result.IsSuccess);
        callLog.Clear();
        fieldPolicy.Decision = PvgPortDecision.FieldSecurityDenied();

        var triaged = await service.TriageDraftAsync(
            new TriageIntakeDraftCommand(
                TenantContext(),
                ActorContext(),
                CorrelationContext(),
                created.IntakeDraftId!,
                new PvgTriageIntakeDraftRequest(PvgTriageOutcome.Rejected, "PVG_TRIAGE_REASON_REJECTED", "triage free-text reason")));

        Assert.False(triaged.Result.IsSuccess);
        Assert.Equal(PvgApplicationOutcome.Blocked, triaged.Result.Outcome);
        Assert.Equal(PvgSafeReasonCodes.FieldSecurityPolicyUnavailable, triaged.Result.ReasonCode);
        Assert.Null(triaged.IntakeDraftId);
        Assert.Null(triaged.AuditIntent);
        Assert.Contains("permission:Triage:pvg.mod0230.intake.triage", callLog);
        Assert.Contains("field:Triage:triage:TriageOutcome", callLog);
        Assert.DoesNotContain("workflow:Triage", callLog);
        Assert.DoesNotContain("evidence:Triage", callLog);

        fieldPolicy.Decision = AllowedDecision();
        var fetched = await service.GetDraftByIdAsync(
            ReadByIdQuery(created.IntakeDraftId!));

        Assert.Single(fetched.Items);
        Assert.Equal(PvgIntakeStatus.IntakeCreated, fetched.Items[0].Status);
        AssertSafe(triaged);
        AssertSafe(fetched);
    }

    [Fact]
    public async Task Cross_tenant_read_and_write_return_safe_not_found_without_existence_leak()
    {
        var callLog = new List<string>();
        var service = NewService(
            new RecordingFieldSecurityPolicy(callLog),
            new RecordingWorkflowTransitionGate(callLog),
            new RecordingEvidenceLinkPort(callLog),
            new RecordingPermissionGate(callLog));
        var created = await service.CreateDraftAsync(CreateCommand(ValidCreateRequest()));
        Assert.True(created.Result.IsSuccess);
        Assert.NotNull(created.IntakeDraftId);

        var foreignTenant = TenantContext("foreign-tenant-secret");
        var crossTenantRead = await service.GetDraftByIdAsync(
            new GetIntakeDraftByIdQuery(foreignTenant, ActorContext(), CorrelationContext(), created.IntakeDraftId!));
        var crossTenantUpdate = await service.UpdateDraftAsync(
            new UpdateIntakeDraftCommand(
                foreignTenant,
                ActorContext(),
                CorrelationContext(),
                created.IntakeDraftId!,
                ValidUpdateRequest()));

        Assert.False(crossTenantRead.Result.IsSuccess);
        Assert.False(crossTenantUpdate.Result.IsSuccess);
        Assert.Equal(PvgApplicationReasonCodes.IntakeDraftNotFound, crossTenantRead.Result.ReasonCode);
        Assert.Equal(PvgApplicationReasonCodes.IntakeDraftNotFound, crossTenantUpdate.Result.ReasonCode);
        Assert.Empty(crossTenantRead.Items);
        Assert.Null(crossTenantUpdate.AuditIntent);
        AssertSafe(crossTenantRead);
        AssertSafe(crossTenantUpdate);
    }

    [Fact]
    public async Task Read_queries_require_actor_correlation_and_permission_before_field_policy_or_store_access()
    {
        var callLog = new List<string>();
        var permissionGate = new RecordingPermissionGate(callLog);
        var service = NewService(
            new RecordingFieldSecurityPolicy(callLog),
            new RecordingWorkflowTransitionGate(callLog),
            new RecordingEvidenceLinkPort(callLog),
            permissionGate);

        var missingActor = await service.ListDraftsAsync(
            new GetIntakeDraftListQuery(TenantContext(), null!, CorrelationContext(), 1, 10, null));
        Assert.False(missingActor.Result.IsSuccess);
        Assert.Equal(PvgPermissionReasonCodes.ActorContextRequired, missingActor.Result.ReasonCode);
        Assert.Empty(callLog);
        Assert.Empty(missingActor.Items);

        var missingCorrelation = await service.GetDraftByIdAsync(
            new GetIntakeDraftByIdQuery(TenantContext(), ActorContext(), null!, "draft-reference"));
        Assert.False(missingCorrelation.Result.IsSuccess);
        Assert.Equal(PvgPermissionReasonCodes.CorrelationContextRequired, missingCorrelation.Result.ReasonCode);
        Assert.Empty(callLog);
        Assert.Empty(missingCorrelation.Items);

        permissionGate.Decision = PvgPermissionDecision.Denied(PvgPermissionReasonCodes.PermissionDenied);
        var deniedList = await service.ListDraftsAsync(ReadListQuery());
        var deniedDetail = await service.GetDraftByIdAsync(ReadByIdQuery("draft-reference"));

        Assert.False(deniedList.Result.IsSuccess);
        Assert.False(deniedDetail.Result.IsSuccess);
        Assert.Equal(PvgPermissionReasonCodes.PermissionDenied, deniedList.Result.ReasonCode);
        Assert.Equal(PvgPermissionReasonCodes.PermissionDenied, deniedDetail.Result.ReasonCode);
        Assert.Equal(
            [
                "permission:GetList:pvg.mod0230.intake.read",
                "permission:GetById:pvg.mod0230.intake.read"
            ],
            callLog);
        Assert.Empty(deniedList.Items);
        Assert.Empty(deniedDetail.Items);
        AssertSafe(missingActor);
        AssertSafe(missingCorrelation);
        AssertSafe(deniedList);
        AssertSafe(deniedDetail);
    }

    [Fact]
    public void Application_service_slice_does_not_add_forbidden_operation_contracts()
    {
        var applicationTypeNames = typeof(PvgIntakeDraftApplicationService)
            .Assembly
            .GetTypes()
            .Select(type => type.Name)
            .ToArray();

        Assert.DoesNotContain(applicationTypeNames, name => name.Contains("Archive", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(applicationTypeNames, name => name.Contains("Void", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(applicationTypeNames, name => name.Contains("Export", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(applicationTypeNames, name => name.Contains("Delete", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(applicationTypeNames, name => name.Contains("BulkDelete", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(applicationTypeNames, name => name.EndsWith("Controller", StringComparison.Ordinal));
        Assert.DoesNotContain(applicationTypeNames, name => name.Contains("Repository", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(applicationTypeNames, name => name.Contains("DbContext", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(applicationTypeNames, name => name.Contains("Mongo", StringComparison.OrdinalIgnoreCase));
    }

    private static PvgIntakeDraftApplicationService NewService(
        IPvgFieldSecurityPolicy fieldPolicy,
        IPvgWorkflowTransitionGate workflowGate,
        IPvgEvidenceLinkPort evidencePort,
        IPvgPermissionGate? permissionGate = null) =>
        new(
            fieldPolicy,
            workflowGate,
            evidencePort,
            permissionGate ?? new RecordingPermissionGate([]),
            new InMemoryPvgIntakeDraftRepository());

    private static PvgServerTenantContext TenantContext(string tenantId = "tenant-secret-123") => new(tenantId);

    private static PvgActorContext ActorContext() => new("actor-secret-456", "HumanUser");

    private static PvgCorrelationContext CorrelationContext() => new("corr-secret-789");

    private static CreateIntakeDraftCommand CreateCommand(PvgCreateIntakeDraftRequest request) =>
        new(TenantContext(), ActorContext(), CorrelationContext(), request);

    private static GetIntakeDraftByIdQuery ReadByIdQuery(string intakeDraftId) =>
        new(TenantContext(), ActorContext(), CorrelationContext(), intakeDraftId);

    private static GetIntakeDraftListQuery ReadListQuery() =>
        new(TenantContext(), ActorContext(), CorrelationContext(), 1, 10, null);

    private static PvgCreateIntakeDraftRequest ValidCreateRequest() =>
        new(
            "Portal",
            "Reporter",
            "source-ref",
            DateTimeOffset.UnixEpoch,
            "HealthcareProfessional",
            "reporter@example.test",
            "patient-subject-code",
            DateOnly.FromDateTime(DateTime.UnixEpoch),
            "free text narrative with PHI",
            "suspect product",
            "Serious",
            "High",
            ["evidence-ref"]);

    private static PvgCreateIntakeDraftRequest InvalidCreateRequest() =>
        ValidCreateRequest() with
        {
            IntakeChannel = "",
            SourceType = "",
            ReceivedAtUtc = null,
            ReporterType = "",
            AdverseEventNarrative = "",
            Seriousness = "",
            IntakePriority = ""
        };

    private static PvgUpdateIntakeDraftRequest ValidUpdateRequest() =>
        new(
            "Portal",
            "Reporter",
            "updated-source-ref",
            DateTimeOffset.UnixEpoch,
            "HealthcareProfessional",
            "reporter@example.test",
            "patient-subject-code",
            DateOnly.FromDateTime(DateTime.UnixEpoch),
            "free text narrative with PHI",
            "suspect product update",
            "Serious",
            "High",
            ["evidence-ref"]);

    private static PvgPortDecision AllowedDecision() => new(true, true, "PVG_TEST_ALLOWED");

    private static void AssertSafe(object result)
    {
        var rendered = result.ToString();
        foreach (var sensitiveSample in SensitiveSamples)
        {
            Assert.DoesNotContain(sensitiveSample, rendered, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static void AssertFieldSecurityBlocked(object result)
    {
        switch (result)
        {
            case PvgIntakeDraftMutationResult mutation:
                Assert.False(mutation.Result.IsSuccess);
                Assert.Equal(PvgApplicationOutcome.Blocked, mutation.Result.Outcome);
                Assert.Equal(PvgSafeReasonCodes.FieldSecurityPolicyUnavailable, mutation.Result.ReasonCode);
                AssertSafeReasonCode(mutation.Result.ReasonCode);
                Assert.Empty(mutation.Result.ValidationFailures);
                Assert.Null(mutation.IntakeDraftId);
                Assert.Null(mutation.AuditIntent);
                break;
            case PvgIntakeDraftQueryResult query:
                Assert.False(query.Result.IsSuccess);
                Assert.Equal(PvgApplicationOutcome.Blocked, query.Result.Outcome);
                Assert.Equal(PvgSafeReasonCodes.FieldSecurityPolicyUnavailable, query.Result.ReasonCode);
                AssertSafeReasonCode(query.Result.ReasonCode);
                Assert.Empty(query.Result.ValidationFailures);
                Assert.Empty(query.Items);
                break;
            default:
                throw new InvalidOperationException($"Unsupported blocked result type {result.GetType().Name}.");
        }
    }

    private static void AssertSafeReasonCode(string? reasonCode)
    {
        Assert.False(string.IsNullOrWhiteSpace(reasonCode));
        Assert.StartsWith("PVG_", reasonCode, StringComparison.Ordinal);
        Assert.All(reasonCode, character =>
            Assert.True(
                char.IsUpper(character) || char.IsDigit(character) || character == '_',
                $"Reason code '{reasonCode}' must contain only safe uppercase token characters."));
    }

    private sealed class RecordingFieldSecurityPolicy(List<string> callLog) : IPvgFieldSecurityPolicy
    {
        public PvgPortDecision Decision { get; set; } = AllowedDecision();

        public ValueTask<PvgPortDecision> EvaluateAsync(
            PvgFieldSecurityRequest request,
            CancellationToken cancellationToken = default)
        {
            callLog.Add($"field:{request.Operation}:{request.Surface}:{request.FieldName}");
            return ValueTask.FromResult(Decision);
        }
    }

    private sealed class RecordingWorkflowTransitionGate(List<string> callLog) : IPvgWorkflowTransitionGate
    {
        public PvgPortDecision Decision { get; set; } = AllowedDecision();

        public ValueTask<PvgPortDecision> EvaluateAsync(
            PvgWorkflowTransitionRequest request,
            CancellationToken cancellationToken = default)
        {
            callLog.Add($"workflow:{request.Operation}");
            return ValueTask.FromResult(Decision);
        }
    }

    private sealed class RecordingEvidenceLinkPort(List<string> callLog) : IPvgEvidenceLinkPort
    {
        public PvgPortDecision Decision { get; set; } = AllowedDecision();

        public ValueTask<PvgPortDecision> EvaluateAsync(
            PvgEvidenceLinkRequest request,
            CancellationToken cancellationToken = default)
        {
            callLog.Add($"evidence:{request.Operation}");
            return ValueTask.FromResult(Decision);
        }
    }

    private sealed class RecordingPermissionGate(List<string> callLog) : IPvgPermissionGate
    {
        public PvgPermissionDecision Decision { get; set; } = PvgPermissionDecision.Allowed();

        public ValueTask<PvgPermissionDecision> EvaluateAsync(
            PvgPermissionRequest request,
            CancellationToken cancellationToken = default)
        {
            callLog.Add($"permission:{request.Operation}:{request.RequiredPermission}");
            return ValueTask.FromResult(Decision);
        }
    }
}
