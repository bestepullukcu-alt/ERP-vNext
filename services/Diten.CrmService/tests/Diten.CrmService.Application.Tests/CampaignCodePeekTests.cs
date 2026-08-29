using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.Campaign;
using Diten.CrmService.Application.Features.Campaign.Handlers;
using Diten.CrmService.Application.Features.Campaign.Queries;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Xunit;
using CampaignEntity = Diten.CrmService.Domain.Entities.Campaign;

namespace Diten.CrmService.Application.Tests;

/// <summary>
/// MOD-0165 FU11 — the create form's CampaignCode placeholder.
///
/// <para>The one property that matters here is that <b>peeking is not generating</b>. The whole reason the FU10
/// generator refused to run when a form opened was that it would burn a sequence number for every abandoned form; a
/// peek that quietly incremented would reintroduce exactly that bug behind a friendlier name. So these tests pin the
/// counter down: the sequence is never advanced, peeking twice answers the same, and the code a peek showed is still
/// the code generation hands out afterwards.</para>
///
/// <para>They also pin the honest failure: no free candidate is answered with NO DATA, not with a code that would not
/// be the one assigned. The create path itself is untouched — an empty CampaignCode still asks the server to assign
/// at save, which is why a stale peek can never become a duplicate.</para>
/// </summary>
public sealed class CampaignCodePeekTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset In2026 = new(2026, 3, 9, 0, 0, 0, TimeSpan.Zero);

    private static TenantContext Tenant(Guid id)
    {
        var ctx = new TenantContext();
        ctx.SetTenant(id);
        return ctx;
    }

    private sealed class FakeCampaignRepo : ICampaignRepository
    {
        public List<CampaignEntity> Items { get; } = new();

        public Task<CampaignEntity?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(c => c.TenantId == t && c.Id == id && !c.IsDeleted));

        public Task<IReadOnlyList<CampaignEntity>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<CampaignEntity>)Items.Where(c => c.TenantId == t).ToList());

        public Task<CampaignEntity?> GetActiveByCodeAsync(Guid t, string code, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(c => c.TenantId == t && c.CampaignCode == code && !c.IsArchived()));

        public Task<CampaignEntity?> FindByExternalReferenceAsync(Guid t, string s, string e, CancellationToken ct)
            => Task.FromResult<CampaignEntity?>(null);

        public Task InsertAsync(CampaignEntity campaign, CancellationToken ct)
        {
            Items.Add(campaign);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(CampaignEntity campaign, CancellationToken ct) => Task.CompletedTask;
    }

    private static CampaignCodeGenerator Generator(
        CampaignScopeTestDoubles.FakeCampaignCodeSequence sequence, FakeCampaignRepo campaigns)
        => new(sequence, campaigns, () => In2026);

    private static CampaignEntity Occupying(string code) => new()
    {
        TenantId = TenantA,
        CampaignCode = code,
        CampaignName = code,
        CampaignType = "awareness",
        StartDate = In2026
    };

    [Fact]
    public async Task Peek_does_not_consume_a_sequence_number()
    {
        var sequence = new CampaignScopeTestDoubles.FakeCampaignCodeSequence();
        var generator = Generator(sequence, new FakeCampaignRepo());

        var first = await generator.PeekAsync(TenantA, CancellationToken.None);
        var second = await generator.PeekAsync(TenantA, CancellationToken.None);

        // Same answer twice, and the counter was never advanced: opening the create form a hundred times must leave
        // no gaps in the sequence. This is the FU10 rule the peek is allowed to bend only because it does not write.
        Assert.Equal("CMP-2026-000001", first!.CampaignCode);
        Assert.Equal(first.CampaignCode, second!.CampaignCode);
        Assert.Equal(0, sequence.Calls);
        Assert.Equal(2, sequence.PeekCalls);
    }

    [Fact]
    public async Task Peeked_code_is_the_one_generation_then_hands_out()
    {
        var sequence = new CampaignScopeTestDoubles.FakeCampaignCodeSequence();
        var generator = Generator(sequence, new FakeCampaignRepo());

        var peeked = await generator.PeekAsync(TenantA, CancellationToken.None);
        var generated = await generator.GenerateAsync(TenantA, CancellationToken.None);

        // The hint is honest when nothing else happened in between — and it stays only a hint, because the create
        // form posts an EMPTY code and this same generation still runs at save.
        Assert.Equal(peeked!.CampaignCode, generated);
    }

    [Fact]
    public async Task Peek_skips_a_code_an_active_campaign_already_holds()
    {
        var sequence = new CampaignScopeTestDoubles.FakeCampaignCodeSequence();
        var campaigns = new FakeCampaignRepo();
        campaigns.Items.Add(Occupying("CMP-2026-000001"));

        var peeked = await PeekWith(sequence, campaigns);

        // A hand-typed code took the slot, so the hint walks past it exactly as generation would. The walk is in
        // memory: skipping still costs no sequence number.
        Assert.Equal("CMP-2026-000002", peeked!.CampaignCode);
        Assert.Equal(2L, peeked.Sequence);
        Assert.Equal(0, sequence.Calls);
    }

    [Fact]
    public async Task Peek_answers_nothing_rather_than_a_code_it_cannot_stand_behind()
    {
        var sequence = new CampaignScopeTestDoubles.FakeCampaignCodeSequence();
        var campaigns = new FakeCampaignRepo();
        for (var i = 1; i <= 5; i++)
        {
            campaigns.Items.Add(Occupying(CampaignCodeGenerator.Format(2026, i)));
        }

        var peeked = await PeekWith(sequence, campaigns);

        // Every candidate in the budget is taken. Showing the last one anyway would advertise a code the save would
        // not assign, so the form opens with no hint at all — and creating still works.
        Assert.Null(peeked);
    }

    [Fact]
    public async Task Handler_requires_a_tenant_and_never_touches_the_sequence_without_one()
    {
        var sequence = new CampaignScopeTestDoubles.FakeCampaignCodeSequence();
        var handler = new PeekNextCampaignCodeHandler(new TenantContext(), Generator(sequence, new FakeCampaignRepo()));

        var response = await handler.Handle(new PeekNextCampaignCodeQuery(), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Equal(0, sequence.PeekCalls);
    }

    [Fact]
    public async Task Handler_returns_the_peeked_code()
    {
        var sequence = new CampaignScopeTestDoubles.FakeCampaignCodeSequence();
        var handler = new PeekNextCampaignCodeHandler(Tenant(TenantA), Generator(sequence, new FakeCampaignRepo()));

        var response = await handler.Handle(new PeekNextCampaignCodeQuery(), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal("CMP-2026-000001", response.Data!.CampaignCode);
        Assert.Equal(2026, response.Data.Year);
        Assert.Equal(0, sequence.Calls);
    }

    private static Task<CampaignCodePeek?> PeekWith(
        CampaignScopeTestDoubles.FakeCampaignCodeSequence sequence, FakeCampaignRepo campaigns)
        => Generator(sequence, campaigns).PeekAsync(TenantA, CancellationToken.None);
}
