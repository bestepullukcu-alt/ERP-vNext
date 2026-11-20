using Diten.MdmService.Application.Behaviors;
using Diten.MdmService.Application.Features.BankMaster;
using Diten.MdmService.Application.Features.BankMaster.Handlers.QueryHandlers;
using Diten.MdmService.Application.Features.BankMaster.Queries;
using Diten.MdmService.Application.Features.BankMaster.Validators;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;
using Xunit;

namespace Diten.MdmService.Application.Tests.BankMaster;

/// <summary>MOD-0289 DL-1 (KARAR D51/4, AC-59) — the bank list's search is plain text: escaped, case-insensitive, 100 characters, tenant-bound.</summary>
public sealed class BankSearchMongoTests
{
    private static async Task<string[]> SearchAsync(BankHarness h, string text)
    {
        var response = await h.List().Handle(new GetBankListQuery { Search = text }, default);
        Assert.True(response.IsSuccessful, string.Join(",", response.Errors)); // 200: no regex error, no 500
        return response.Data!.Items.Select(i => i.Name).Order(StringComparer.Ordinal).ToArray();
    }

    [Fact]
    public async Task Mod0289Dl1_AC59_Search_matches_text_not_a_pattern_ignores_case_and_never_leaves_the_tenant()
    {
        var h = new BankHarness();
        foreach (var name in new[] { "Acme.Bank", "AcmeXBank", "Odd.*Name", "Paren(Bank", "[A-Z]Bracket", "Back\\slash", "Plain" })
            await h.NewBankAsync(name);
        var foreign = new BankHarness();
        await foreign.NewBankAsync("Acme.Bank");
        await foreign.NewBankAsync("Foreign.*Only");

        // "." is a dot, not "any character": acme.bank finds Acme.Bank and NOT AcmeXBank.
        Assert.Equal(["Acme.Bank"], await SearchAsync(h, "acme.bank"));
        // Case does not matter.
        Assert.Equal(["Acme.Bank"], await SearchAsync(h, "ACME.BANK"));
        Assert.Equal(["Acme.Bank", "AcmeXBank"], await SearchAsync(h, "ACME"));
        // Metacharacters find only what contains them — not everything, not an error.
        Assert.Equal(["Odd.*Name"], await SearchAsync(h, ".*"));
        Assert.Equal(["Paren(Bank"], await SearchAsync(h, "("));
        Assert.Equal(["[A-Z]Bracket"], await SearchAsync(h, "[A-Z]"));
        Assert.Equal(["Back\\slash"], await SearchAsync(h, "\\"));
        Assert.Empty(await SearchAsync(h, "Foreign"));
        Assert.Equal(["Foreign.*Only"], await SearchAsync(foreign, ".*"));
        // No search: the tenant's own list, all of it and only it.
        Assert.Equal(7, (await h.List().Handle(new GetBankListQuery(), default)).Data!.Total);
    }

    [Fact]
    public async Task Mod0289Dl1_AC59_A_hundred_characters_is_searched_and_one_more_is_refused_before_the_store_is_asked()
    {
        var h = new BankHarness();
        var hundred = new string('x', 100);
        await h.NewBankAsync(hundred);
        var spy = new SpyBanks();
        var behavior = new ValidationBehavior<GetBankListQuery, Response<BankListResultDto>>([new GetBankListValidator()]);

        // 100: through the pipeline, to the real store, and found.
        var ok = await behavior.Handle(new GetBankListQuery { Search = hundred },
            () => new GetBankListHandler(h.Banks).Handle(new GetBankListQuery { Search = hundred }, default), default);
        Assert.True(ok.IsSuccessful);
        Assert.Single(ok.Data!.Items);

        // 101: 400 MDM_BANK_VALIDATION_FAILED and the store is never asked.
        var asked = false;
        var tooLong = await behavior.Handle(new GetBankListQuery { Search = hundred + "x" }, () =>
        {
            asked = true;
            return new GetBankListHandler(spy).Handle(new GetBankListQuery { Search = hundred + "x" }, default);
        }, default);
        Assert.False(tooLong.IsSuccessful);
        Assert.Equal(400, tooLong.StatusCode);
        Assert.StartsWith(BankErrorCodes.ValidationFailed, tooLong.Errors.Single());
        Assert.False(asked);
        Assert.Equal(0, spy.ListCalls);
    }

    private sealed class SpyBanks : IBankRepository
    {
        public int ListCalls { get; private set; }

        public Task<BankPage> ListAsync(BankListCriteria criteria, CancellationToken cancellationToken = default)
        {
            ListCalls++;
            return Task.FromResult(new BankPage([], 0));
        }

        public Task<Bank?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Bank>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<BankStoreOutcome> CreateAsync(Bank bank, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<BankStoreOutcome> UpdateAsync(Bank bank, int expectedVersion, LocalAuditIntent intent, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<BankStoreOutcome> SoftDeleteAsync(Bank bank, int expectedVersion, LocalAuditIntent intent, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<BankStoreOutcome> SoftDeleteManyAsync(IReadOnlyList<BankSoftDelete> items, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
