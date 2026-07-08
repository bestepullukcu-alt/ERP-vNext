using System.Text.Json;
using Diten.HcmService.Application.Common;
using Diten.HcmService.Application.Features.CoreHrEmployeeMaster;
using Diten.HcmService.Application.Features.CoreHrEmployeeMaster.Commands;
using Diten.HcmService.Application.Features.CoreHrEmployeeMaster.Handlers;
using Diten.HcmService.Application.Features.CoreHrEmployeeMaster.Validators;
using Diten.HcmService.Domain.Entities;
using Diten.HcmService.Domain.Repositories;
using Xunit;

namespace Diten.HcmService.Application.Tests;

public sealed class EmployeeDraftBackendTests
{
    private readonly TenantContext _tenantContext = new();
    private readonly InMemoryEmployeeDraftSessionRepository _repository = new();
    private readonly RecordingDraftAuditService _auditService = new();
    private readonly StaticReferenceValidationClient _referenceValidationClient = new();

    public EmployeeDraftBackendTests()
    {
        _tenantContext.SetTenant(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
    }

    [Fact]
    public async Task CreateDraft_ReplaysSameIdempotencyKey_WithoutDuplicateDraft()
    {
        var handler = new CreateEmployeeDraftHandler(_tenantContext, _repository, _auditService);
        var request = new CreateEmployeeDraftCommand(new EmployeeDraftCreateRequest("wizard", "client-1", "create-key"));

        var first = await handler.Handle(request, CancellationToken.None);
        var second = await handler.Handle(request, CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.True(second.IsSuccessful);
        Assert.Equal(first.Data!.DraftSessionId, second.Data!.DraftSessionId);
        Assert.Equal(1, _repository.Count);
        Assert.Single(_auditService.Events);
        Assert.Equal("employee_draft.created", _auditService.Events[0].EventName);
    }

    [Fact]
    public async Task PatchDraft_WithStaleETag_ReturnsConflict()
    {
        var draftSession = await CreateDraftAsync();
        var handler = new PatchEmployeeDraftHandler(_tenantContext, _repository, _auditService);
        var payload = JsonPayload(new Dictionary<string, object?> { ["person_id"] = "person-1" });

        var response = await handler.Handle(
            new PatchEmployeeDraftCommand(
                draftSession.Id,
                "\"stale\"",
                new EmployeeDraftPatchRequest("profile", "employee-create-wizard.v1", payload, null, "patch-key")),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
    }

    [Fact]
    public void PatchDraftValidator_RejectsGovernmentIdentifierPayload()
    {
        var validator = new PatchEmployeeDraftValidator();
        var payload = JsonPayload(new Dictionary<string, object?> { ["national_id"] = "123456789" });
        var command = new PatchEmployeeDraftCommand(
            Guid.NewGuid(),
            "\"1\"",
            new EmployeeDraftPatchRequest("profile", "employee-create-wizard.v1", payload, null, "patch-key"));

        var result = validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("Government identifier", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateReferences_StoresSummary_AndReviewRemainsNonSubmit()
    {
        var draftSession = await CreateDraftAsync();
        await SaveCompleteDraftPayloadAsync(draftSession);
        var reloaded = await _repository.GetByIdAsync(_tenantContext.TenantId!.Value, draftSession.Id, CancellationToken.None);
        Assert.NotNull(reloaded);

        var validateHandler = new ValidateDraftReferencesHandler(_tenantContext, _repository, _referenceValidationClient, _auditService);
        var validation = await validateHandler.Handle(
            new ValidateDraftReferencesCommand(
                draftSession.Id,
                reloaded!.ETag,
                new ReferenceValidationRequest("person-1", "org-1", "position-1", "legal-1", "validate-key")),
            CancellationToken.None);

        Assert.True(validation.IsSuccessful);
        Assert.True(validation.Data!.CanReview);

        var afterValidation = await _repository.GetByIdAsync(_tenantContext.TenantId!.Value, draftSession.Id, CancellationToken.None);
        var reviewHandler = new ReviewEmployeeDraftHandler(_tenantContext, _repository, _auditService);
        var review = await reviewHandler.Handle(
            new ReviewEmployeeDraftCommand(
                draftSession.Id,
                afterValidation!.ETag,
                new DraftReviewRequest("review-key", true, true, afterValidation.ETag)),
            CancellationToken.None);

        Assert.True(review.IsSuccessful);
        Assert.Equal("reviewed", review.Data!.ReviewState);
        Assert.False(review.Data.CanSubmitLater);
        Assert.Empty(review.Data.BlockingReasons);
        Assert.Contains(_auditService.Events, auditEvent => auditEvent.EventName == "employee_draft.references_validated");
        Assert.Contains(_auditService.Events, auditEvent => auditEvent.EventName == "employee_draft.reviewed");
    }

    [Fact]
    public async Task SubmitDraft_IsBlockedByCurrentP2Scope_WithoutStartingWorkflow()
    {
        var draftSession = await CreateDraftAsync();
        await SaveCompleteDraftPayloadAsync(draftSession);
        var afterSave = await _repository.GetByIdAsync(_tenantContext.TenantId!.Value, draftSession.Id, CancellationToken.None);
        var validateHandler = new ValidateDraftReferencesHandler(_tenantContext, _repository, _referenceValidationClient, _auditService);
        await validateHandler.Handle(
            new ValidateDraftReferencesCommand(
                draftSession.Id,
                afterSave!.ETag,
                new ReferenceValidationRequest("person-1", "org-1", "position-1", "legal-1", "validate-submit-key")),
            CancellationToken.None);

        var afterValidation = await _repository.GetByIdAsync(_tenantContext.TenantId!.Value, draftSession.Id, CancellationToken.None);
        var reviewHandler = new ReviewEmployeeDraftHandler(_tenantContext, _repository, _auditService);
        await reviewHandler.Handle(
            new ReviewEmployeeDraftCommand(
                draftSession.Id,
                afterValidation!.ETag,
                new DraftReviewRequest("review-submit-key", true, true, afterValidation.ETag)),
            CancellationToken.None);

        var reviewed = await _repository.GetByIdAsync(_tenantContext.TenantId!.Value, draftSession.Id, CancellationToken.None);
        var submitHandler = new SubmitEmployeeDraftHandler();

        var submit = await submitHandler.Handle(
            new SubmitEmployeeDraftCommand(
                draftSession.Id,
                reviewed!.ETag,
                new DraftSubmitRequest("submit-key", reviewed.ETag)),
            CancellationToken.None);

        Assert.False(submit.IsSuccessful);
        Assert.Equal(409, submit.StatusCode);
        Assert.Contains(SubmitEmployeeDraftHandler.ScopeBlockedReason, submit.Errors!);
        Assert.Null(submit.Data);

        var changed = await _repository.GetByIdAsync(_tenantContext.TenantId!.Value, draftSession.Id, CancellationToken.None);
        Assert.Equal(reviewed.Version, changed!.Version);
        Assert.Equal(reviewed.ETag, changed.ETag);
    }

    [Fact]
    public void SubmitDraftValidator_RequiresIfMatchAndIdempotencyKey()
    {
        var validator = new SubmitEmployeeDraftValidator();
        var result = validator.Validate(new SubmitEmployeeDraftCommand(Guid.Empty, null, new DraftSubmitRequest(string.Empty, null)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "DraftSessionId");
        Assert.Contains(result.Errors, error => error.PropertyName == "IfMatch");
        Assert.Contains(result.Errors, error => error.PropertyName == "Request.IdempotencyKey");
    }

    [Fact]
    public async Task GetDraft_IsTenantScoped()
    {
        var draftSession = await CreateDraftAsync();
        _tenantContext.SetTenant(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
        var handler = new GetEmployeeDraftHandler(_tenantContext, _repository);

        var response = await handler.Handle(new Diten.HcmService.Application.Features.CoreHrEmployeeMaster.Queries.GetEmployeeDraftQuery(draftSession.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    private async Task<EmployeeDraftSession> CreateDraftAsync()
    {
        var handler = new CreateEmployeeDraftHandler(_tenantContext, _repository, _auditService);
        var response = await handler.Handle(
            new CreateEmployeeDraftCommand(new EmployeeDraftCreateRequest("wizard", "client", Guid.NewGuid().ToString())),
            CancellationToken.None);

        return (await _repository.GetByIdAsync(_tenantContext.TenantId!.Value, response.Data!.DraftSessionId, CancellationToken.None))!;
    }

    private async Task SaveCompleteDraftPayloadAsync(EmployeeDraftSession draftSession)
    {
        var handler = new PatchEmployeeDraftHandler(_tenantContext, _repository, _auditService);
        var payload = JsonPayload(new Dictionary<string, object?>
        {
            ["person_id"] = "02510000-0000-0000-0000-000000000001",
            ["employee_number"] = "MOD0251-TEST-001",
            ["legal_name"] = "Example Person",
            ["worker_type"] = "employee",
            ["employment_type"] = "full_time",
            ["hire_date"] = "2026-06-18",
            ["organization_unit_id"] = "02510000-0000-0000-0000-000000000002",
            ["position_id"] = "02510000-0000-0000-0000-000000000003",
            ["legal_entity_id"] = "02510000-0000-0000-0000-000000000004",
            ["sensitivity_level"] = "standard"
        });

        var response = await handler.Handle(
            new PatchEmployeeDraftCommand(
                draftSession.Id,
                draftSession.ETag,
                new EmployeeDraftPatchRequest("employment", "employee-create-wizard.v1", payload, null, "patch-complete-key")),
            CancellationToken.None);

        Assert.True(response.IsSuccessful);
    }

    private static Dictionary<string, JsonElement> JsonPayload(Dictionary<string, object?> values)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(values));
        return document.RootElement.EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.Clone(), StringComparer.OrdinalIgnoreCase);
    }

    private sealed class InMemoryEmployeeDraftSessionRepository : IEmployeeDraftSessionRepository
    {
        private readonly Dictionary<Guid, EmployeeDraftSession> _sessions = new();

        public int Count => _sessions.Count;

        public Task<EmployeeDraftSession?> GetByIdAsync(Guid tenantId, Guid draftSessionId, CancellationToken cancellationToken)
        {
            _sessions.TryGetValue(draftSessionId, out var session);
            return Task.FromResult(session is not null && session.TenantId == tenantId && !session.IsDeleted ? session : null);
        }

        public Task<EmployeeDraftSession?> GetByCreateIdempotencyKeyAsync(Guid tenantId, string idempotencyKeyHash, CancellationToken cancellationToken)
        {
            return Task.FromResult(_sessions.Values.FirstOrDefault(session =>
                session.TenantId == tenantId
                && session.CreateIdempotencyKeyHash == idempotencyKeyHash
                && !session.IsDeleted));
        }

        public Task AddAsync(EmployeeDraftSession draftSession, CancellationToken cancellationToken)
        {
            _sessions[draftSession.Id] = draftSession;
            return Task.CompletedTask;
        }

        public Task<bool> ReplaceAsync(EmployeeDraftSession draftSession, int expectedVersion, CancellationToken cancellationToken)
        {
            if (!_sessions.TryGetValue(draftSession.Id, out var existing) || existing.Version != draftSession.Version)
            {
                _sessions[draftSession.Id] = draftSession;
                return Task.FromResult(true);
            }

            return expectedVersion == draftSession.Version - 1
                ? Task.FromResult(true)
                : Task.FromResult(false);
        }
    }

    private sealed class RecordingDraftAuditService : IDraftAuditService
    {
        public List<DraftAuditEvent> Events { get; } = [];

        public Task EmitAsync(DraftAuditEvent auditEvent, CancellationToken cancellationToken)
        {
            Events.Add(auditEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class StaticReferenceValidationClient : IReferenceValidationClient
    {
        public Task<ReferenceValidationItem> ValidatePersonAsync(string? personId, CancellationToken cancellationToken)
            => Task.FromResult(Valid("person", personId, "MOD-0288"));

        public Task<ReferenceValidationItem> ValidateOrganizationUnitAsync(string? organizationUnitId, CancellationToken cancellationToken)
            => Task.FromResult(Valid("organization_unit", organizationUnitId, "MOD-0288"));

        public Task<ReferenceValidationItem> ValidatePositionAsync(string? positionId, CancellationToken cancellationToken)
            => Task.FromResult(Valid("position", positionId, "MOD-0288"));

        public Task<ReferenceValidationItem> ValidateLegalEntityAsync(string? legalEntityId, CancellationToken cancellationToken)
            => Task.FromResult(Valid("legal_entity", legalEntityId, "MDM"));

        private static ReferenceValidationItem Valid(string referenceType, string? referenceId, string provider)
            => new(referenceType, referenceId ?? string.Empty, "valid", true, provider, null, new Dictionary<string, string>());
    }
}
