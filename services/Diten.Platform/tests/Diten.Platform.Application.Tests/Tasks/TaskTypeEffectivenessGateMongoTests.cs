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

    [Fact]
    public async Task Creating_a_type_with_a_blocked_bound_document_is_refused_and_nothing_is_persisted()
    {
        // Measured (this WP's own NE): a type is born IsActive=true and CAN already carry GroupDocuments at
        // creation — the gate that guards /active must therefore guard creation too, or it is a bypass.
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var (types, port, tenantId) = await ArrangeAsync(mongo, ("DOC-DRAFT-2", ControlledDocumentLifecycleStatus.Draft));
        var tenant = new TenantContext();
        tenant.SetTenant(tenantId);
        var request = new CreateTaskTypeRequest(
            "GOV", "Governed at birth", null, TaskRecordClass.NOT_A_RECORD, null, null, false,
            GroupDocuments: ["DOC-DRAFT-2"], LocalDocuments: null);

        var result = await new CreateTaskTypeHandler(types, tenant, new Mock<ICurrentUserContext>().Object, port)
            .Handle(new CreateTaskTypeCommand(request, "c"), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(TaskReasonCodes.TaskTypeEnableBlockedDocuments, result.ReasonCode);
        Assert.Null(await types.GetByCodeAsync("GOV"));
    }

    [Fact]
    public async Task Creating_a_type_with_an_effective_bound_document_succeeds()
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
        Assert.NotNull(await types.GetByCodeAsync("GOVOK"));
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
