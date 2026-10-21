using Diten.BuildingBlocks.Auditing;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Domain.Entities.Commercial;
using MediatR;

namespace Diten.CrmService.Application.Features.Commercial.PreOrders;

/// <summary>
/// WP-VW-W6 W6-BE-c1 — MOD-0168 §12: a failed transmission is tried again by the scheduler with a growing wait (5, 10, 20,
/// 40 … minutes, at most 6 hours) while the tenant's <c>TransmissionRetryMax</c> (0–10, default 5) lasts; after that only
/// the authorised manual retry (<c>commercial.pre-orders.retry-transmission</c>) moves it. The same attempt never opens a
/// second e-mail: its number is part of the idempotency key.
/// </summary>
public static class PreOrderTransmissionRetry
{
    public const int BatchSize = 50;
    public static readonly TimeSpan FirstWait = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan MaxWait = TimeSpan.FromHours(6);

    /// <summary>The wait after the n-th failure.</summary>
    public static TimeSpan Backoff(int failures)
    {
        var wait = TimeSpan.FromTicks(FirstWait.Ticks * (long)Math.Pow(2, Math.Clamp(failures - 1, 0, 20)));
        return wait > MaxWait ? MaxWait : wait;
    }
}

/// <summary>The scheduler's retry of ONE due transmission, in that order's tenant (the scheduler pins it). Audited as the
/// system (no user behind it); a row that is no longer due answers false and changes nothing.</summary>
public sealed record RunPreOrderTransmissionRetryCommand(Guid Id) : IRequest<Response<bool>>, IAuditableCommand, IAuditDetailsProvider
{
    public AuditDescriptor DescribeAudit() => PreOrderAudit.Describe(AuditOperation.Execute, PreOrderAudit.PreOrderEntity, Id);

    public void DescribeAuditDetails(AuditDetails details, object? response)
        => details.Set("channel", "scheduler").Set("ran", (response as Response<bool>)?.Data);
}

public sealed class RunPreOrderTransmissionRetryHandler : IRequestHandler<RunPreOrderTransmissionRetryCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly PreOrderContext _c;
    private readonly PreOrderTransmissionService _transmission;

    public RunPreOrderTransmissionRetryHandler(ITenantContext tenant, PreOrderContext context, PreOrderTransmissionService transmission)
    {
        _tenant = tenant;
        _c = context;
        _transmission = transmission;
    }

    public async Task<Response<bool>> Handle(RunPreOrderTransmissionRetryCommand request, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return PreOrderResults.Invalid<bool>("Tenant context is required.");
        }

        var order = await _c.Orders.GetByIdAsync(tenantId, request.Id, ct);
        if (order is null || order.TenantId != tenantId || order.IsDeleted
            || order.Status != PreOrderStatuses.TransmissionFailed
            || order.Transmission?.NextAttemptAtTicks is not { } due || due > _c.Now.UtcTicks)
        {
            return Response<bool>.Success(false);
        }

        return Response<bool>.Success(await _transmission.TransmitAsync(order, null, ct));
    }
}
