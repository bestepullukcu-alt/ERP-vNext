using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Commercial.PreOrders;
using Diten.CrmService.Application.Features.Commercial.PreOrders.Commands;
using Diten.CrmService.Application.Tests.Commercial.Pricing;
using Diten.CrmService.Domain.Entities.Commercial;
using Xunit;

namespace Diten.CrmService.Application.Tests.Commercial.PreOrders;

/// <summary>The steps every MOD-0168 test takes (draft → signature → submission → decision), on a fresh world.</summary>
public abstract class PreOrderTestBase : IDisposable
{
    internal readonly PreOrderWorld W = new();

    public void Dispose() => W.Dispose();

    internal const string Png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

    protected static PreOrderLineInput Line(string sku, int quantity = 20, (int Buy, int Free)? mf = null, decimal? discount = null)
        => new(PricingWorld.Sku(sku), quantity, mf is { } m ? new PreOrderMfInput(m.Buy, m.Free) : null, discount);

    protected static PreOrderInput Input(Guid? depot = null, Guid? pharmacy = null, params PreOrderLineInput[] lines)
        => new(pharmacy ?? PreOrderWorld.Pharmacy, depot ?? PreOrderWorld.Depot, null, "Kasım siparişi", null,
            lines.Length == 0 ? new[] { Line("TUT", 20, (10, 2), 5m) } : lines);

    protected static string Code<T>(Response<T> r) => r.Errors?.FirstOrDefault() ?? string.Empty;

    protected static T Ok<T>(Response<T> r)
    {
        Assert.True(r.IsSuccessful, string.Join(" | ", r.Errors ?? []));
        return r.Data!;
    }

    internal async Task<PreOrderDto> Draft(Guid? rep = null, PreOrderInput? input = null)
        => Ok(await W.Send(new CreatePreOrderCommand(input ?? Input()), rep ?? PreOrderWorld.Rep));

    internal async Task<PreOrderDto> Sign(Guid id, Guid? rep = null)
        => Ok(await W.Send(new CapturePreOrderSignatureCommand(id, new PreOrderSignatureInput("Ecz. Ali Veli", Png, "image/png")), rep ?? PreOrderWorld.Rep));

    internal Task<Response<PreOrderDto>> Submit(Guid id, Guid? rep = null)
        => W.Send(new SubmitPreOrderCommand(id, new PreOrderVersionInput()), rep ?? PreOrderWorld.Rep);

    /// <summary>A signed draft sent to approval (the default tenant setting: always).</summary>
    internal async Task<PreOrderDto> Pending(Guid? rep = null, PreOrderInput? input = null)
    {
        var draft = await Draft(rep, input);
        await Sign(draft.Id, rep);
        var submitted = Ok(await Submit(draft.Id, rep));
        Assert.Equal(PreOrderStatuses.PendingApproval, submitted.Status);
        return submitted;
    }

    internal Task<Response<PreOrderDto>> Decide(Guid id, string decision, string? reason = null, Guid? by = null, string? token = null)
        => W.Send(new DecidePreOrderCommand(id, new PreOrderDecisionInput(decision, reason, token)), by ?? PreOrderWorld.Manager);

    internal async Task<PreOrderDto> Approved(Guid? rep = null)
    {
        var pending = await Pending(rep);
        return Ok(await Decide(pending.Id, PreOrderDecisions.Approve));
    }

    internal Task SetMode(string mode)
    {
        var s = W.Settings.Store.Single();
        s.ApprovalMode = mode;
        return Task.CompletedTask;
    }
}
