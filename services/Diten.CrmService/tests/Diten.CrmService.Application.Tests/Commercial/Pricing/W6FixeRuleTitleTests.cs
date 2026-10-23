using Diten.CrmService.Application.Features.Commercial.Pricing.Contracts;
using Diten.CrmService.Application.Features.Commercial.Pricing.PricingRules;
using Diten.CrmService.Application.Features.Commercial.Pricing.PricingRules.Commands;
using Xunit;

namespace Diten.CrmService.Application.Tests.Commercial.Pricing;

/// <summary>
/// WP-VW-W6 W6-FIX-e (G-14) — the Task Center title of a rule sent for approval names the PRODUCT (the rule's own SKU name
/// snapshot) and the selling company; the SKU code only when the snapshot has no name. Only the new submission is titled
/// so — an open task is never rewritten.
/// </summary>
public sealed class W6FixeRuleTitleTests : IDisposable
{
    private readonly PricingWorld _w = new();

    public void Dispose() => _w.Dispose();

    private static PricingRuleInput Rule()
        => new(PricingWorld.LeTr, PricingWorld.Sku("TUT"), null, null, null, new PricingPctLimitInput(5, 10),
            new PricingMfLimitInput(new PricingMfTermInput(10, 1), new PricingMfTermInput(10, 2)), new PricingPctLimitInput(12, 20),
            new PricingQuantityLimitInput(1, 50, 100));

    private async Task<string> SubmittedTitle(Action<Guid>? before = null)
    {
        var draft = await _w.Send(new CreatePricingRuleCommand(Rule()), PricingWorld.Marketing);
        Assert.True(draft.IsSuccessful, string.Join(" | ", draft.Errors ?? []));
        before?.Invoke(draft.Data!.Id);
        var submitted = await _w.Send(new SubmitPricingRuleCommand(draft.Data!.Id, null), PricingWorld.Marketing);
        Assert.True(submitted.IsSuccessful, string.Join(" | ", submitted.Errors ?? []));
        return _w.Workflow.Starts.Last().DisplayContext.Title;
    }

    [Fact]
    public async Task T1_the_approval_title_is_the_product_name_and_the_selling_company()
        => Assert.Equal("TUTUKON 100 mg 28 tablet · Grand Medical TR", await SubmittedTitle());

    [Fact]
    public async Task T2_without_a_name_the_title_falls_back_to_the_sku_code()
    {
        var title = await SubmittedTitle(id => _w.Rules.Store.Single(r => r.Id == id).SkuName = " ");

        Assert.Equal("LS-TUT · Grand Medical TR", title);
    }
}
