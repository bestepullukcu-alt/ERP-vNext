using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Queries;
using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Services;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using MediatR;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

using Task = System.Threading.Tasks.Task;

/// <summary>
/// WP-DM-DCP005-BL380-KURAL4-01 (Kural 4, DCP-005 Adım 3, G3 — sahip 2026-09-15, Kalite teyidi bekliyor) —
/// <see cref="Diten.Platform.Application.Features.Tasks.Services.TaskTypeEffectivenessGate"/> against a REAL
/// Document Master Register: the task type repository and the register repository are two REAL Mongo repositories
/// sharing one tenant/database, and <see cref="ResolveDocumentEffectivenessQuery"/> runs through its REAL handler
/// (<see cref="ResolveDocumentEffectivenessHandler"/>) — only IMediator's ROUTING is a test double
/// (<see cref="SingleQueryMediator"/>), never the resolution logic under test, the same posture the Meetings
/// module's own cross-command real-Mongo tests already take.
/// </summary>
[Collection(DisposableMongoReplicaSetCollection.Name)]
public sealed class TaskTypeEffectivenessGateMongoTests
{
    [Fact]
    public async Task Activate_succeeds_when_every_bound_document_is_Effective()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var (types, port, tenantId) = await ArrangeAsync(mongo, ("DOC-1", ControlledDocumentLifecycleStatus.Effective));
        var type = new TaskType { TenantId = tenantId, Code = "CNC", Name = "Governed", IsActive = false, GroupDocuments = ["DOC-1"] };
        await types.CreateAsync(type);

        var result = await new SetTaskTypeActiveHandler(types, port).Handle(
            new SetTaskTypeActiveCommand(type.Id, new SetTaskTypeActiveRequest(true, type.Version), "c"), CancellationToken.None);

        Assert.True(result.IsSuccessful, string.Join(" | ", result.Errors));
        Assert.True((await types.GetByIdAsync(type.Id))!.IsActive);
    }

    [Fact]
    public async Task Activate_is_refused_when_a_bound_document_is_not_Effective()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var (types, port, tenantId) = await ArrangeAsync(mongo, ("DOC-DRAFT", ControlledDocumentLifecycleStatus.Draft));
        var type = new TaskType { TenantId = tenantId, Code = "CNC", Name = "Governed", IsActive = false, GroupDocuments = ["DOC-DRAFT"] };
        await types.CreateAsync(type);

        var result = await new SetTaskTypeActiveHandler(types, port).Handle(
            new SetTaskTypeActiveCommand(type.Id, new SetTaskTypeActiveRequest(true, type.Version), "c"), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(TaskReasonCodes.TaskTypeEnableBlockedDocuments, result.ReasonCode);
        Assert.Contains(result.Errors, e => e.Contains("DOC-DRAFT", StringComparison.Ordinal) && e.Contains("Draft", StringComparison.Ordinal));
        Assert.False((await types.GetByIdAsync(type.Id))!.IsActive); // refused write never lands
    }

    [Fact]
    public async Task Activate_is_refused_when_a_bound_document_has_no_register_row_at_all()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var (types, port, tenantId) = await ArrangeAsync(mongo); // register left empty
        var type = new TaskType { TenantId = tenantId, Code = "CNC", Name = "Governed", IsActive = false, GroupDocuments = ["DOC-GHOST"] };
        await types.CreateAsync(type);

        var result = await new SetTaskTypeActiveHandler(types, port).Handle(
            new SetTaskTypeActiveCommand(type.Id, new SetTaskTypeActiveRequest(true, type.Version), "c"), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(TaskReasonCodes.TaskTypeEnableBlockedDocuments, result.ReasonCode);
        Assert.Contains(result.Errors, e => e.Contains("DOC-GHOST", StringComparison.Ordinal) && e.Contains("Unresolved", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Activate_checks_LocalDocuments_too_not_only_GroupDocuments()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var (types, port, tenantId) = await ArrangeAsync(mongo, ("DOC-LOCAL", ControlledDocumentLifecycleStatus.Retired));
        var type = new TaskType
        {
            TenantId = tenantId, Code = "CNC", Name = "Governed", IsActive = false,
            LocalDocuments = new Dictionary<string, List<string>> { ["ORG-A"] = ["DOC-LOCAL"] }
        };
        await types.CreateAsync(type);

        var result = await new SetTaskTypeActiveHandler(types, port).Handle(
            new SetTaskTypeActiveCommand(type.Id, new SetTaskTypeActiveRequest(true, type.Version), "c"), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(TaskReasonCodes.TaskTypeEnableBlockedDocuments, result.ReasonCode);
    }

    [Fact]
    public async Task A_type_with_no_bound_documents_at_all_activates_freely()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var (types, port, tenantId) = await ArrangeAsync(mongo);
        var type = new TaskType { TenantId = tenantId, Code = "CNC", Name = "Ungoverned", IsActive = false };
        await types.CreateAsync(type);

        var result = await new SetTaskTypeActiveHandler(types, port).Handle(
            new SetTaskTypeActiveCommand(type.Id, new SetTaskTypeActiveRequest(true, type.Version), "c"), CancellationToken.None);

        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public async Task Deactivating_is_never_gated_even_with_a_blocked_bound_document()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var (types, port, tenantId) = await ArrangeAsync(mongo, ("DOC-RETIRED", ControlledDocumentLifecycleStatus.Retired));
        var type = new TaskType { TenantId = tenantId, Code = "CNC", Name = "Governed", IsActive = true, GroupDocuments = ["DOC-RETIRED"] };
        await types.CreateAsync(type);

        var result = await new SetTaskTypeActiveHandler(types, port).Handle(
            new SetTaskTypeActiveCommand(type.Id, new SetTaskTypeActiveRequest(false, type.Version), "c"), CancellationToken.None);

        Assert.True(result.IsSuccessful, string.Join(" | ", result.Errors));
        Assert.False((await types.GetByIdAsync(type.Id))!.IsActive);
    }

    [Fact]
    public async Task Reactivating_an_ALREADY_active_type_never_re_checks_its_documents()
    {
        // An already-active type asked to be "activated" again (IsActive: true posted on an active row) must not
        // suddenly retire itself over a document that went Blocked AFTER it first went live — Kural 4 gates the
        // pasif→aktif EDGE only.
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var (types, port, tenantId) = await ArrangeAsync(mongo, ("DOC-NOW-RETIRED", ControlledDocumentLifecycleStatus.Retired));
        var type = new TaskType { TenantId = tenantId, Code = "CNC", Name = "Governed", IsActive = true, GroupDocuments = ["DOC-NOW-RETIRED"] };
        await types.CreateAsync(type);

        var result = await new SetTaskTypeActiveHandler(types, port).Handle(
            new SetTaskTypeActiveCommand(type.Id, new SetTaskTypeActiveRequest(true, type.Version), "c"), CancellationToken.None);

        Assert.True(result.IsSuccessful, string.Join(" | ", result.Errors));
    }

    /// <summary>
    /// Kural 4 v2 (sahip 2026-09-15, Blueprint/SAP/Oracle kıyasıyla doğrulandı — WP-CT-DECISION-BENCHMARK-01) —
    /// SUPERSEDES this WP's own v1 behavior. Creation is NEVER refused for a document reason any more: a type
    /// born with a non-Effective bound document is saved — 201, not 409 — but PASSIVE, and the response NAMES
    /// which document blocked it. Measured (this WP's own NE): a type is born IsActive=true by default and CAN
    /// already carry GroupDocuments at creation, so this is the ONLY moment creation itself is gated at all.
    /// </summary>
    [Fact]
    public async Task Creating_a_type_with_a_blocked_bound_document_succeeds_but_is_saved_inactive_naming_the_blocker()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var (types, port, tenantId) = await ArrangeAsync(mongo, ("DOC-DRAFT-2", ControlledDocumentLifecycleStatus.Draft));
        var tenant = new TenantContext();
        tenant.SetTenant(tenantId);
        var request = new CreateTaskTypeRequest(
            "GOV", "Governed at birth", null, TaskRecordClass.NOT_A_RECORD, null, null, false,
            GroupDocuments: ["DOC-DRAFT-2"], LocalDocuments: null);

        var result = await new CreateTaskTypeHandler(types, tenant, new Mock<ICurrentUserContext>().Object, port)
            .Handle(new CreateTaskTypeCommand(request, "c"), CancellationToken.None);

        Assert.True(result.IsSuccessful, string.Join(" | ", result.Errors));
        Assert.Equal(201, result.StatusCode);
        Assert.False(result.Data!.IsActive);
        Assert.False(result.Data.EffectivenessUnavailable);
        Assert.Contains(result.Data.BlockingDocuments, e => e.Contains("DOC-DRAFT-2", StringComparison.Ordinal) && e.Contains("Draft", StringComparison.Ordinal));
        var stored = await types.GetByCodeAsync("GOV");
        Assert.NotNull(stored);
        Assert.False(stored!.IsActive);
    }

    [Fact]
    public async Task Creating_a_type_with_an_effective_bound_document_succeeds_active()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var (types, port, tenantId) = await ArrangeAsync(mongo, ("DOC-OK", ControlledDocumentLifecycleStatus.Effective));
        var tenant = new TenantContext();
        tenant.SetTenant(tenantId);
        var request = new CreateTaskTypeRequest(
            "GOVOK", "Governed at birth", null, TaskRecordClass.NOT_A_RECORD, null, null, false,
            GroupDocuments: ["DOC-OK"], LocalDocuments: null);

        var result = await new CreateTaskTypeHandler(types, tenant, new Mock<ICurrentUserContext>().Object, port)
            .Handle(new CreateTaskTypeCommand(request, "c"), CancellationToken.None);

        Assert.True(result.IsSuccessful, string.Join(" | ", result.Errors));
        Assert.True(result.Data!.IsActive);
        Assert.Empty(result.Data.BlockingDocuments);
        Assert.False(result.Data.EffectivenessUnavailable);
        var stored = await types.GetByCodeAsync("GOVOK");
        Assert.NotNull(stored);
        Assert.True(stored!.IsActive);
    }

    [Fact]
    public async Task Creating_a_type_when_the_register_is_unreachable_succeeds_but_is_saved_inactive_as_unverified()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var (types, _, tenantId) = await ArrangeAsync(mongo);
        var tenant = new TenantContext();
        tenant.SetTenant(tenantId);
        var throwingPort = new FakeControlledDocumentEffectivenessPort { Throws = true };
        var request = new CreateTaskTypeRequest(
            "GOVUNK", "Governed, unverifiable", null, TaskRecordClass.NOT_A_RECORD, null, null, false,
            GroupDocuments: ["DOC-ANY"], LocalDocuments: null);

        var result = await new CreateTaskTypeHandler(types, tenant, new Mock<ICurrentUserContext>().Object, throwingPort)
            .Handle(new CreateTaskTypeCommand(request, "c"), CancellationToken.None);

        Assert.True(result.IsSuccessful, string.Join(" | ", result.Errors));
        Assert.False(result.Data!.IsActive);
        Assert.True(result.Data.EffectivenessUnavailable);
        Assert.Empty(result.Data.BlockingDocuments);
        var stored = await types.GetByCodeAsync("GOVUNK");
        Assert.NotNull(stored);
        Assert.False(stored!.IsActive);
    }

    [Fact]
    public async Task Creating_a_type_with_no_bound_documents_is_active_regardless_of_the_register()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var (types, port, tenantId) = await ArrangeAsync(mongo);
        var tenant = new TenantContext();
        tenant.SetTenant(tenantId);
        var request = new CreateTaskTypeRequest(
            "GOVNONE", "Ungoverned", null, TaskRecordClass.NOT_A_RECORD, null, null, false,
            GroupDocuments: null, LocalDocuments: null);

        var result = await new CreateTaskTypeHandler(types, tenant, new Mock<ICurrentUserContext>().Object, port)
            .Handle(new CreateTaskTypeCommand(request, "c"), CancellationToken.None);

        Assert.True(result.IsSuccessful, string.Join(" | ", result.Errors));
        Assert.True(result.Data!.IsActive);
    }

    // ── Kural 4 v2 — editing an ACTIVE type's bound documents ───────────────────────────────────────────────

    [Fact]
    public async Task Editing_an_active_types_documents_to_include_a_blocked_one_is_refused_and_nothing_is_written()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var (types, port, tenantId) = await ArrangeAsync(mongo, ("DOC-BAD", ControlledDocumentLifecycleStatus.Draft));
        var type = new TaskType { TenantId = tenantId, Code = "ACT", Name = "Active governed", IsActive = true, GroupDocuments = ["DOC-OLD"] };
        await types.CreateAsync(type);
        var request = new UpdateTaskTypeRequest(
            "ACT", "Renamed while at it", null, TaskRecordClass.NOT_A_RECORD, null, null, false,
            GroupDocuments: ["DOC-BAD"], LocalDocuments: null, ExpectedVersion: type.Version);

        var result = await new UpdateTaskTypeHandler(types, port)
            .Handle(new UpdateTaskTypeCommand(type.Id, request, "c"), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(TaskReasonCodes.TaskTypeEnableBlockedDocuments, result.ReasonCode);
        var stored = await types.GetByIdAsync(type.Id);
        // NOTHING was written — not the document set, and not the unrelated Name this same request carried.
        Assert.Equal(["DOC-OLD"], stored!.GroupDocuments);
        Assert.Equal("Active governed", stored.Name);
        Assert.True(stored.IsActive);
    }

    [Fact]
    public async Task Editing_an_active_types_documents_to_all_Effective_writes_the_change()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var (types, port, tenantId) = await ArrangeAsync(mongo, ("DOC-GOOD", ControlledDocumentLifecycleStatus.Effective));
        var type = new TaskType { TenantId = tenantId, Code = "ACT2", Name = "Active governed", IsActive = true, GroupDocuments = ["DOC-OLD"] };
        await types.CreateAsync(type);
        var request = new UpdateTaskTypeRequest(
            "ACT2", "Active governed", null, TaskRecordClass.NOT_A_RECORD, null, null, false,
            GroupDocuments: ["DOC-GOOD"], LocalDocuments: null, ExpectedVersion: type.Version);

        var result = await new UpdateTaskTypeHandler(types, port)
            .Handle(new UpdateTaskTypeCommand(type.Id, request, "c"), CancellationToken.None);

        Assert.True(result.IsSuccessful, string.Join(" | ", result.Errors));
        var stored = await types.GetByIdAsync(type.Id);
        Assert.Equal(["DOC-GOOD"], stored!.GroupDocuments);
    }

    [Fact]
    public async Task Editing_an_active_types_documents_when_the_register_is_unreachable_is_refused_503()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var (types, _, tenantId) = await ArrangeAsync(mongo);
        var type = new TaskType { TenantId = tenantId, Code = "ACT3", Name = "Active governed", IsActive = true, GroupDocuments = ["DOC-OLD"] };
        await types.CreateAsync(type);
        var throwingPort = new FakeControlledDocumentEffectivenessPort { Throws = true };
        var request = new UpdateTaskTypeRequest(
            "ACT3", "Active governed", null, TaskRecordClass.NOT_A_RECORD, null, null, false,
            GroupDocuments: ["DOC-NEW"], LocalDocuments: null, ExpectedVersion: type.Version);

        var result = await new UpdateTaskTypeHandler(types, throwingPort)
            .Handle(new UpdateTaskTypeCommand(type.Id, request, "c"), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(503, result.StatusCode);
        Assert.Equal(TaskReasonCodes.TaskTypeEnableRegisterUnavailable, result.ReasonCode);
        Assert.Equal(["DOC-OLD"], (await types.GetByIdAsync(type.Id))!.GroupDocuments);
    }

    [Fact]
    public async Task Editing_an_active_types_OTHER_fields_without_touching_its_blocked_documents_is_never_gated()
    {
        // HaveDocumentsChanged's own point: an already-active type whose bound documents predate this Kural (or
        // were bound before they went Blocked) must remain editable for anything that is NOT a document change.
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var (types, port, tenantId) = await ArrangeAsync(mongo, ("DOC-STILL-BAD", ControlledDocumentLifecycleStatus.Retired));
        var type = new TaskType { TenantId = tenantId, Code = "ACT4", Name = "Old name", IsActive = true, GroupDocuments = ["DOC-STILL-BAD"] };
        await types.CreateAsync(type);
        var request = new UpdateTaskTypeRequest(
            "ACT4", "New name, same documents", null, TaskRecordClass.NOT_A_RECORD, null, null, false,
            GroupDocuments: ["DOC-STILL-BAD"], LocalDocuments: null, ExpectedVersion: type.Version);

        var result = await new UpdateTaskTypeHandler(types, port)
            .Handle(new UpdateTaskTypeCommand(type.Id, request, "c"), CancellationToken.None);

        Assert.True(result.IsSuccessful, string.Join(" | ", result.Errors));
        Assert.Equal("New name, same documents", (await types.GetByIdAsync(type.Id))!.Name);
    }

    [Fact]
    public async Task Editing_a_PASSIVE_types_documents_to_a_blocked_one_is_free()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var (types, port, tenantId) = await ArrangeAsync(mongo, ("DOC-BAD2", ControlledDocumentLifecycleStatus.Draft));
        var type = new TaskType { TenantId = tenantId, Code = "PAS", Name = "Passive governed", IsActive = false };
        await types.CreateAsync(type);
        var request = new UpdateTaskTypeRequest(
            "PAS", "Passive governed", null, TaskRecordClass.NOT_A_RECORD, null, null, false,
            GroupDocuments: ["DOC-BAD2"], LocalDocuments: null, ExpectedVersion: type.Version);

        var result = await new UpdateTaskTypeHandler(types, port)
            .Handle(new UpdateTaskTypeCommand(type.Id, request, "c"), CancellationToken.None);

        Assert.True(result.IsSuccessful, string.Join(" | ", result.Errors));
        Assert.Equal(["DOC-BAD2"], (await types.GetByIdAsync(type.Id))!.GroupDocuments);
        Assert.False((await types.GetByIdAsync(type.Id))!.IsActive); // still passive — editing documents never activates
    }

    [Fact]
    public async Task A_port_exception_is_translated_to_503_register_unavailable_fail_closed()
    {
        // Simulates "kütüğe ulaşılamıyor" — the register-read half is not what this test exercises (proven by the
        // other tests in this class against the REAL register); this one proves the gate's own fail-closed
        // exception handling, the same way MeetingInviteMailerNeverThrowsTests isolates ITS OWN failure branch.
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var (types, _, tenantId) = await ArrangeAsync(mongo);
        var type = new TaskType { TenantId = tenantId, Code = "CNC", Name = "Governed", IsActive = false, GroupDocuments = ["DOC-1"] };
        await types.CreateAsync(type);
        var throwingPort = new FakeControlledDocumentEffectivenessPort { Throws = true };

        var result = await new SetTaskTypeActiveHandler(types, throwingPort).Handle(
            new SetTaskTypeActiveCommand(type.Id, new SetTaskTypeActiveRequest(true, type.Version), "c"), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(503, result.StatusCode);
        Assert.Equal(TaskReasonCodes.TaskTypeEnableRegisterUnavailable, result.ReasonCode);
        Assert.False((await types.GetByIdAsync(type.Id))!.IsActive);
    }

    // ── harness ───────────────────────────────────────────────────────────────────────────────────────────────

    private static async Task<(TaskTypeRepository Types, IControlledDocumentEffectivenessPort Port, Guid TenantId)> ArrangeAsync(
        DisposableMongoReplicaSet mongo, params (string Uid, ControlledDocumentLifecycleStatus Status)[] registerRows)
    {
        var tenantId = Guid.NewGuid();
        var database = mongo.CreateDatabase();
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(tenantId);
        var dbContext = new PlatformDbContext(mongo.Client, database);

        var types = new TaskTypeRepository(dbContext, tenantContext);
        var register = new DocumentMasterRegisterRepository(dbContext, tenantContext);
        foreach (var (uid, status) in registerRows)
        {
            await register.CreateAsync(new DocumentMasterRegisterEntry
            {
                TenantId = tenantId, DocumentTitle = uid, PermanentUid = uid, DocumentCode = uid,
                LifecycleStatus = status
            });
        }

        var mediator = new SingleQueryMediator(register, tenantContext);
        var port = new ControlledDocumentEffectivenessPort(mediator);
        return (types, port, tenantId);
    }

    /// <summary>Routes ONLY <see cref="ResolveDocumentEffectivenessQuery"/>, to its REAL handler — the same
    /// "double the routing, never the logic under test" shape the Meetings module's own SweepMediator takes.</summary>
    private sealed class SingleQueryMediator(DocumentMasterRegisterRepository register, ITenantContext tenantContext) : IMediator
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
        {
            if (request is ResolveDocumentEffectivenessQuery query)
            {
                var result = new ResolveDocumentEffectivenessHandler(register, tenantContext).Handle(query, ct);
                return (Task<TResponse>)(object)result;
            }

            throw new NotSupportedException($"SingleQueryMediator does not route {request.GetType().Name}.");
        }

        public Task<object?> Send(object request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : IRequest => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken ct = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default) where TNotification : INotification => throw new NotSupportedException();
    }
}
