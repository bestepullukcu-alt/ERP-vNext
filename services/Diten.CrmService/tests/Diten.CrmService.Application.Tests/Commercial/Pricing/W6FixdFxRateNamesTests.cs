using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Commercial.Pricing.FxRates;
using Xunit;

namespace Diten.CrmService.Application.Tests.Commercial.Pricing;

/// <summary>
/// WP-VW-W6 W6-FIX-d (G-3) — the FX list says WHO changed a rate by name: the page's distinct writer ids are asked in ONE
/// display-name call (never one per row), an id that is not resolved has no name (null — the screen writes "—", never the
/// id), and a resolver that fails leaves the list readable without names. On CRM's real pipeline (PricingWorld).
/// </summary>
public sealed class W6FixdFxRateNamesTests : IDisposable
{
    private static readonly string Ayse = "a1000000-0000-4000-8000-0000000000f1";
    private static readonly string Burak = "a1000000-0000-4000-8000-0000000000f2";
    private static readonly string Unknown = "a1000000-0000-4000-8000-0000000000f9";
    private readonly PricingWorld _w = new();

    public W6FixdFxRateNamesTests()
    {
        _w.Names.Known[Guid.Parse(Ayse)] = "Ayşe Operasyon";
        _w.Names.Known[Guid.Parse(Burak)] = "Burak Operasyon";
    }

    public void Dispose() => _w.Dispose();

    private static FxRateInput Fx(int day, decimal rate) => new(PricingWorld.LeAe, "USD", "AED", new DateOnly(2026, 10, day), rate, "manual");

    private static T Ok<T>(Response<T> r)
    {
        Assert.True(r.IsSuccessful, string.Join(" | ", r.Errors ?? []));
        return r.Data!;
    }

    [Fact]
    public async Task N1_the_list_names_every_writer_with_one_call_for_the_whole_page_and_an_unresolved_writer_has_no_name()
    {
        await _w.Send(new CreateFxRateCommand(Fx(1, 3.6725m)), Ayse);
        await _w.Send(new CreateFxRateCommand(Fx(2, 3.6730m)), Burak);
        var corrected = Ok(await _w.Send(new CreateFxRateCommand(Fx(3, 3.6740m)), Ayse));
        await _w.Send(new UpdateFxRateCommand(corrected.Id, new FxRateInput(null, null, null, null, 3.6800m, null, corrected.Version)), Unknown);
        _w.Names.Calls.Clear();

        var page = Ok(await _w.Send(new GetFxRateListQuery(PricingWorld.LeAe, null, null, null, null, 1, 50), Ayse));

        var call = Assert.Single(_w.Names.Calls);                                                     // ONE call for the page …
        Assert.Equal(new[] { Ayse, Burak, Unknown }.Select(Guid.Parse).Order(), call.Order());        // … with the distinct ids
        var byDay = page.Items.ToDictionary(r => r.RateDate.Day);
        Assert.Equal(("Ayşe Operasyon", "Ayşe Operasyon"), (byDay[1].CreatedByName, byDay[1].UpdatedByName));
        Assert.Equal(("Burak Operasyon", "Burak Operasyon"), (byDay[2].CreatedByName, byDay[2].UpdatedByName));
        Assert.Equal(("Ayşe Operasyon", (string?)null), (byDay[3].CreatedByName, byDay[3].UpdatedByName)); // not resolved → no name
        Assert.Equal(Unknown, byDay[3].UpdatedBy);                                                    // the id stays the id field's …
        Assert.All(page.Items, r => Assert.DoesNotContain(r.UpdatedByName ?? string.Empty, new[] { Ayse, Burak, Unknown }));
        Assert.All(page.Items, r => Assert.DoesNotContain(r.CreatedByName ?? string.Empty, new[] { Ayse, Burak, Unknown })); // … never a name
    }

    [Fact]
    public async Task N2_a_failing_resolver_leaves_the_list_readable_without_names_and_a_write_answers_with_the_name()
    {
        var created = Ok(await _w.Send(new CreateFxRateCommand(Fx(1, 3.6725m)), Ayse));
        Assert.Equal(("Ayşe Operasyon", "Ayşe Operasyon"), (created.CreatedByName, created.UpdatedByName));

        _w.Names.Fail = true;
        var page = Ok(await _w.Send(new GetFxRateListQuery(PricingWorld.LeAe, null, null, null, null, 1, 50), Ayse));

        var row = Assert.Single(page.Items);
        Assert.Equal(((string?)null, (string?)null, Ayse), (row.CreatedByName, row.UpdatedByName, row.UpdatedBy));
    }

    [Fact]
    public async Task N3_an_empty_page_and_a_writer_that_is_not_a_user_id_ask_nothing()
    {
        Ok(await _w.Send(new GetFxRateListQuery(PricingWorld.LeAe, null, null, null, null, 1, 50), Ayse));
        await _w.Send(new CreateFxRateCommand(Fx(1, 3.6725m)), "salesops@example.test");
        _w.Names.Calls.Clear();

        var row = Assert.Single(Ok(await _w.Send(new GetFxRateListQuery(PricingWorld.LeAe, null, null, null, null, 1, 50), Ayse)).Items);

        Assert.Empty(_w.Names.Calls);
        Assert.Null(row.UpdatedByName);
    }
}

/// <summary>The display-name seam as AuthService answers it: known ids only, every call recorded; <see cref="Fail"/> throws.</summary>
internal sealed class CountingUserNames : IUserDisplayNameResolver
{
    public Dictionary<Guid, string> Known { get; } = new();
    public List<IReadOnlyCollection<Guid>> Calls { get; } = new();
    public bool Fail { get; set; }

    public Task<IReadOnlyDictionary<Guid, string>> ResolveAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default)
    {
        Calls.Add(userIds.ToList());
        if (Fail)
        {
            throw new HttpRequestException("AuthService unreachable");
        }

        return Task.FromResult<IReadOnlyDictionary<Guid, string>>(Known.Where(k => userIds.Contains(k.Key)).ToDictionary(k => k.Key, k => k.Value));
    }
}
