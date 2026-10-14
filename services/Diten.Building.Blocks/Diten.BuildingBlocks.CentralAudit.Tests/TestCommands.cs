using MediatR;

namespace Diten.BuildingBlocks.CentralAudit.Tests;

/// <summary>The repo's envelope shape: a public <c>bool IsSuccessful</c>.</summary>
public sealed class Result<T>
{
    public T? Data { get; init; }
    public bool IsSuccessful { get; init; }

    public static Result<T> Ok(T data) => new() { Data = data, IsSuccessful = true };
    public static Result<T> Fail() => new() { IsSuccessful = false };
}

public sealed record WidgetView(Guid WidgetId);

/// <summary>How the test handler ends.</summary>
public enum Ending
{
    Succeeds,
    AnswersFailure,
    Throws,

    /// <summary>Enlists in a transaction that commits, then succeeds.</summary>
    TransactionCommitsThenSucceeds,

    /// <summary>Enlists in a transaction that aborts, then answers a failure (the ledger's "insufficient stock").</summary>
    TransactionAbortsThenFails,

    /// <summary>One transaction commits, a later step fails (multi-transaction command).</summary>
    TransactionCommitsThenFails,

    /// <summary>Enlists in two committed transactions (e.g. ledger posting + quarantine intake), then succeeds.</summary>
    TwoTransactionsThenSucceeds,

    /// <summary>Sends a nested marked command, then succeeds.</summary>
    SendsNestedThenSucceeds
}

/// <summary>
/// A marked write command. <see cref="Secret"/> and <see cref="TenantIdInBody"/> are request-body fields: the central record
/// must carry neither (no body copy; the tenant comes from the resolved scope, never from the body).
/// </summary>
public sealed record CreateWidgetCommand(Ending Ending, string Secret = "s3cr3t-body-value", Guid TenantIdInBody = default)
    : IRequest<Result<WidgetView>>, ICentralAuditDeclaration
{
    public static readonly Guid CreatedId = Guid.Parse("50150150-0000-4000-8000-0000000000e1");

    public CentralAuditMetadata CentralAudit => new("MasterData", "Create", "Widget", "widget-catalog");

    public Guid? CentralAuditEntityId(object? response) => (response as Result<WidgetView>)?.Data?.WidgetId;
}

public sealed record NestedWidgetCommand : IRequest<Result<WidgetView>>, ICentralAuditDeclaration
{
    public static readonly Guid NestedId = Guid.Parse("50150150-0000-4000-8000-0000000000e2");

    public CentralAuditMetadata CentralAudit => new("MasterData", "Update", "WidgetPart", "widget-catalog");

    public Guid? CentralAuditEntityId(object? response) => NestedId;
}

/// <summary>Changes something in another module over HTTP: no transaction of its own (RequireEnlistment services).</summary>
public sealed record PublishWidgetTemplateCommand : IRequest<Result<WidgetView>>, ICentralAuditOutsideTransaction
{
    public CentralAuditMetadata CentralAudit => new("MasterData", "Create", "WidgetTemplate", "widget-catalog");

    public Guid? CentralAuditEntityId(object? response) => (response as Result<WidgetView>)?.Data?.WidgetId;
}

internal sealed class PublishWidgetTemplateHandler : IRequestHandler<PublishWidgetTemplateCommand, Result<WidgetView>>
{
    public Task<Result<WidgetView>> Handle(PublishWidgetTemplateCommand request, CancellationToken ct) =>
        Task.FromResult(Result<WidgetView>.Ok(new WidgetView(CreateWidgetCommand.CreatedId)));
}

/// <summary>Not marked: must leave nothing.</summary>
public sealed record RenameWidgetCommand : IRequest<Result<WidgetView>>;

internal sealed class CreateWidgetHandler(ICentralAuditTransaction audit, ISender sender) : IRequestHandler<CreateWidgetCommand, Result<WidgetView>>
{
    public async Task<Result<WidgetView>> Handle(CreateWidgetCommand request, CancellationToken ct)
    {
        switch (request.Ending)
        {
            case Ending.Throws:
                throw new InvalidOperationException("handler failed (test)");
            case Ending.AnswersFailure:
                return Result<WidgetView>.Fail();
            case Ending.TransactionCommitsThenSucceeds:
                await InTransactionAsync(commit: true);
                return Result<WidgetView>.Ok(new WidgetView(CreateWidgetCommand.CreatedId));
            case Ending.TransactionAbortsThenFails:
                await InTransactionAsync(commit: false);
                return Result<WidgetView>.Fail();
            case Ending.TransactionCommitsThenFails:
                await InTransactionAsync(commit: true);
                throw new InvalidOperationException("second step failed (test)");
            case Ending.TwoTransactionsThenSucceeds:
                await InTransactionAsync(commit: true);
                await InTransactionAsync(commit: true);
                return Result<WidgetView>.Ok(new WidgetView(CreateWidgetCommand.CreatedId));
            case Ending.SendsNestedThenSucceeds:
                await sender.Send(new NestedWidgetCommand(), ct);
                return Result<WidgetView>.Ok(new WidgetView(CreateWidgetCommand.CreatedId));
            default:
                return Result<WidgetView>.Ok(new WidgetView(CreateWidgetCommand.CreatedId));
        }

        async Task InTransactionAsync(bool commit)
        {
            var session = FakeSession.Start();
            await audit.EnlistAsync(session.Handle, CreateWidgetCommand.CreatedId, ct);
            if (commit)
            {
                session.Commit();
            }
            else
            {
                session.Abort();
            }
        }
    }
}

/// <summary>Enlists through the static, constructor-free form a repository uses (CentralAuditTransaction).</summary>
internal sealed class NestedWidgetHandler : IRequestHandler<NestedWidgetCommand, Result<WidgetView>>
{
    public async Task<Result<WidgetView>> Handle(NestedWidgetCommand request, CancellationToken ct)
    {
        var session = FakeSession.Start();
        await CentralAuditTransaction.EnlistAsync(session.Handle, NestedWidgetCommand.NestedId, ct);
        session.Commit();
        return Result<WidgetView>.Ok(new WidgetView(NestedWidgetCommand.NestedId));
    }
}

internal sealed class RenameWidgetHandler : IRequestHandler<RenameWidgetCommand, Result<WidgetView>>
{
    public Task<Result<WidgetView>> Handle(RenameWidgetCommand request, CancellationToken ct) =>
        Task.FromResult(Result<WidgetView>.Ok(new WidgetView(Guid.NewGuid())));
}
