using MediatR;

namespace Diten.PlanningService.Application.Features.Mrp;

public enum LeadTimeOperation { Create, Revise, Submit, Approve, Reject }
public enum LeadTimeStatus { Created, Updated, NotFound, Forbidden, Conflict, Invalid, Unavailable }

public sealed record LeadTimeCommand(
    LeadTimeOperation Operation, Guid TenantId, Guid LegalEntityId, Guid SkuId,
    Guid WarehouseId, Guid ActorId, Guid PolicyId, Guid RevisionId,
    int? LeadTimeDays, DateOnly? EffectiveFrom, DateOnly? EffectiveUntil,
    long? ExpectedVersion, string? Reason, string IdempotencyKey) : IRequest<LeadTimeResult>;

public sealed record LeadTimeReceipt(Guid PolicyId, Guid RevisionId, string State, long Version);
public sealed record LeadTimeResult(LeadTimeStatus Status, LeadTimeReceipt? Receipt = null);

public interface ILeadTimeAuthority
{
    Task<bool> CanExecuteAsync(LeadTimeCommand command, string permission, CancellationToken cancellationToken);
}

public sealed class UnavailableLeadTimeAuthority : ILeadTimeAuthority
{
    public Task<bool> CanExecuteAsync(LeadTimeCommand command, string permission, CancellationToken cancellationToken) =>
        Task.FromResult(false);
}

public interface ILeadTimeMutationStore
{
    Task<LeadTimeResult> ExecuteAsync(LeadTimeCommand command, CancellationToken cancellationToken);
}

public sealed class LeadTimeCommandHandler(ILeadTimeAuthority authority, ILeadTimeMutationStore store)
    : IRequestHandler<LeadTimeCommand, LeadTimeResult>
{
    public Task<LeadTimeResult> Handle(LeadTimeCommand command, CancellationToken cancellationToken) =>
        HandleAsync(command, cancellationToken);

    public async Task<LeadTimeResult> HandleAsync(LeadTimeCommand command, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(command.Operation)) return new(LeadTimeStatus.Invalid);
        var permission = command.Operation switch
        {
            LeadTimeOperation.Create => "mrp.lead-times.create",
            LeadTimeOperation.Revise => "mrp.lead-times.update",
            LeadTimeOperation.Submit => "mrp.lead-times.submit",
            LeadTimeOperation.Approve or LeadTimeOperation.Reject => "mrp.lead-times.review",
            _ => throw new ArgumentOutOfRangeException(nameof(command))
        };
        try
        {
            return await authority.CanExecuteAsync(command, permission, cancellationToken)
                ? await store.ExecuteAsync(command, cancellationToken)
                : new(LeadTimeStatus.Forbidden);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return new(LeadTimeStatus.Unavailable);
        }
    }
}
