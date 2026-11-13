using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.VisitQr;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Diten.CrmService.Persistence.Repositories;
using MongoDB.Driver;
using Xunit;

namespace Diten.CrmService.Application.Tests.VisitExecution;

/// <summary>
/// WP-VW-W3E · W3E-CITY (K-CITY a) — the QR list's city filter on the PRODUCTION rule (<see cref="CityRefMatcher"/>), the
/// repository default and the list handler, and the Mongo filter on a real mongod (CRM_TEST_MONGO_URI, never the dev port):
/// a reference-data code (konya) finds TR-42-KONYA, a full reference still matches exactly, Turkish letters and case fold,
/// a part of a name never matches, regex characters in the input are inert, another tenant's institution never comes back.
/// </summary>
public sealed class VwW3ECityTests
{
    private static readonly Guid Tenant = Guid.Parse("97c50000-0000-0000-0000-0000000000c1");
    private static readonly Guid OtherTenant = Guid.Parse("11110000-0000-0000-0000-0000000000c1");
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 9, 0, 0, TimeSpan.Zero);

    // ═══ the rule ════════════════════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("konya", "TR-42-KONYA", true)]
    [InlineData("istanbul", "TR-34-ISTANBUL", true)]
    [InlineData("adiyaman", "TR-02-ADIYAMAN", true)]
    [InlineData("sanliurfa", "TR-63-SANLIURFA", true)]
    [InlineData("izmir", "TR-35-IZMIR", true)]
    [InlineData("TR-42-KONYA", "TR-42-KONYA", true)]          // the full reference, exactly — as before
    [InlineData("tr-42-konya", "TR-42-KONYA", true)]          // case-insensitive
    [InlineData("KONYA", "TR-42-KONYA", true)]                // upper-case code
    [InlineData("İstanbul", "TR-34-ISTANBUL", true)]          // Turkish letters fold to ASCII
    [InlineData("Şanlıurfa", "TR-63-SANLIURFA", true)]
    [InlineData("Adıyaman", "TR-02-ADIYAMAN", true)]
    [InlineData("istanbul", "TR-34-İSTANBUL", true)]          // a stored NAME with Turkish letters still matches
    [InlineData("afyonkarahisar", "TR-03-AFYON-KARAHISAR", true)] // a hyphen inside NAME is optional (Web's cityLabel twin)
    [InlineData(" konya ", "TR-42-KONYA", true)]              // trimmed
    [InlineData("kon", "TR-42-KONYA", false)]                 // a part of a name never matches
    [InlineData("onya", "TR-42-KONYA", false)]
    [InlineData("konyaa", "TR-42-KONYA", false)]
    [InlineData("ankara", "TR-42-KONYA", false)]              // another city
    [InlineData("konya", "TR-42-KONYA-EREGLI", false)]        // a longer NAME is a different place
    [InlineData("konya", "konya", false)]                     // a bare code is not an account reference shape
    [InlineData("TR-42-KONYA", "TR-42-KONYA-X", false)]       // the full reference is exact
    [InlineData("TR-42", "TR-42-KONYA", false)]
    [InlineData("konya", null, false)]
    [InlineData("", "TR-42-KONYA", false)]
    [InlineData("   ", "TR-42-KONYA", false)]
    public void City_rule_matches_the_reference_data_code_and_the_full_reference(string input, string? cityRef, bool expected)
        => Assert.Equal(expected, CityRefMatcher.Matches(input, cityRef));

    [Theory]
    [InlineData(".*")]
    [InlineData("^.*$")]
    [InlineData("kon.*")]
    [InlineData("k.nya")]
    [InlineData("konya|ankara")]
    [InlineData("(konya)")]
    [InlineData("[k]onya")]
    [InlineData("kon\\w+")]
    [InlineData("TR-42-.*")]
    [InlineData("TR-4.-KONYA")]
    [InlineData("$where")]
    [InlineData("((((((((((")]
    public void City_rule_regex_characters_in_the_input_are_inert(string input)
    {
        var pattern = CityRefMatcher.Pattern(input);
        Assert.NotNull(pattern);
        Assert.StartsWith("^", pattern);
        Assert.EndsWith("$", pattern);
        _ = new System.Text.RegularExpressions.Regex(pattern!);             // always a valid regex
        Assert.False(CityRefMatcher.Matches(input, "TR-42-KONYA"));
        Assert.False(CityRefMatcher.Matches(input, "TR-06-ANKARA"));
    }

    [Fact]
    public void City_rule_the_patterns_are_anchored_and_the_fold_is_turkish_aware()
    {
        Assert.Equal("^TR-42-KONYA$", CityRefMatcher.Pattern("TR-42-KONYA"));
        Assert.Equal("^" + CityRefMatcher.RefPrefixPattern + "k-?[oOöÖ]-?n-?y-?a$", CityRefMatcher.Pattern("konya"));
        Assert.Equal("istanbul", CityRefMatcher.Fold("İSTANBUL"));
        Assert.Equal("sanliurfa", CityRefMatcher.Fold("ŞANLIURFA"));
        Assert.Equal("cankiri", CityRefMatcher.Fold("Çankırı"));
        Assert.Equal("gumushane", CityRefMatcher.Fold("Gümüşhane"));
        Assert.True(CityRefMatcher.IsFullRef("TR-42-KONYA"));
        Assert.False(CityRefMatcher.IsFullRef("konya"));
        Assert.Null(CityRefMatcher.Pattern(" "));
        Assert.Null(CityRefMatcher.Pattern("---"));
    }

    // ═══ the repository default + the list handler ══════════════════════════════════════════════════════

    [Fact]
    public async Task City_the_list_with_city_konya_answers_only_this_tenants_konya_institutions()
    {
        var accounts = new CityAccounts();
        var k1 = accounts.Add("Konya Şehir Hastanesi", "TR-42-KONYA");
        var k2 = accounts.Add("Selçuklu Eczanesi", "tr-42-konya");
        accounts.Add("Ankara Şehir Hastanesi", "TR-06-ANKARA");
        accounts.Add("Konya Ereğli Devlet", "TR-42-KONYA-EREGLI");
        accounts.Add("Kurum (şehirsiz)", null);
        accounts.Add("Başka kiracı Konya", "TR-42-KONYA", OtherTenant);   // never listed

        IAccountRepository repo = accounts;
        Assert.Equal(new[] { k1.Id, k2.Id }.OrderBy(x => x), (await repo.ListIdsByCityAsync(Tenant, "konya", default)).OrderBy(x => x));
        Assert.Equal(new[] { k1.Id, k2.Id }.OrderBy(x => x), (await repo.ListIdsByCityAsync(Tenant, "TR-42-KONYA", default)).OrderBy(x => x));
        Assert.Empty(await repo.ListIdsByCityAsync(Tenant, "kon", default));

        var handler = new ListVisitQrAccountsHandler(Service(accounts), null!, null!, null!);
        foreach (var city in new[] { "konya", "KONYA", "TR-42-KONYA" })
        {
            var page = (await handler.Handle(new ListVisitQrAccountsQuery(null, null, city, null, null, 1, 25), default)).Data!;
            Assert.Equal(2, page.Total);
            Assert.Equal(new[] { "Konya Şehir Hastanesi", "Selçuklu Eczanesi" }, page.Items.Select(i => i.AccountName).OrderBy(n => n, StringComparer.Ordinal));
        }

        var istanbul = (await handler.Handle(new ListVisitQrAccountsQuery(null, null, "istanbul", null, null, 1, 25), default)).Data!;
        Assert.Equal(0, istanbul.Total);
        var part = (await handler.Handle(new ListVisitQrAccountsQuery(null, null, "kon", null, null, 1, 25), default)).Data!;
        Assert.Equal(0, part.Total);
    }

    // ═══ the Mongo filter (a real mongod on the lane's own port; skipped without CRM_TEST_MONGO_URI) ═════════════

    [Fact]
    public async Task City_the_mongo_filter_matches_the_code_and_the_reference_in_one_tenant_and_is_injection_safe()
    {
        var uri = Environment.GetEnvironmentVariable("CRM_TEST_MONGO_URI");
        if (string.IsNullOrWhiteSpace(uri) || uri.Contains(":27017", StringComparison.Ordinal)) return; // never the dev database
        Diten.CrmService.Persistence.DependencyInjection.EnsureClassMapsForTests();
        var client = new MongoClient(uri);
        var name = "W3E_CITY_" + Guid.NewGuid().ToString("N");
        try
        {
            var db = client.GetDatabase(name);
            var repo = new AccountRepository(db);
            Account Make(string city, Guid? tenant = null, bool deleted = false) => new()
            {
                Id = Guid.NewGuid(), TenantId = tenant ?? Tenant, AccountName = "K " + city, AccountCode = "C" + Guid.NewGuid().ToString("N")[..6], CityRef = city, IsDeleted = deleted
            };
            var konya = Make("TR-42-KONYA");
            var konyaLower = Make("tr-42-konya");
            var istanbulTr = Make("TR-34-İSTANBUL");
            var istanbul = Make("TR-34-ISTANBUL");
            var urfa = Make("TR-63-SANLIURFA");
            var afyon = Make("TR-03-AFYON-KARAHISAR");
            var eregli = Make("TR-42-KONYA-EREGLI");
            var foreign = Make("TR-42-KONYA", OtherTenant);
            var gone = Make("TR-42-KONYA", deleted: true);
            foreach (var a in new[] { konya, konyaLower, istanbulTr, istanbul, urfa, afyon, eregli, foreign, gone }) await repo.InsertAsync(a, default);

            async Task<Guid[]> Ids(string input) => (await repo.ListIdsByCityAsync(Tenant, input, default)).OrderBy(x => x).ToArray();
            Guid[] Of(params Account[] a) => a.Select(x => x.Id).OrderBy(x => x).ToArray();

            Assert.Equal(Of(konya, konyaLower), await Ids("konya"));
            Assert.Equal(Of(konya, konyaLower), await Ids("Konya"));
            Assert.Equal(Of(konya, konyaLower), await Ids("TR-42-KONYA"));       // the full reference — as before
            Assert.Equal(Of(istanbulTr, istanbul), await Ids("istanbul"));
            Assert.Equal(Of(istanbulTr, istanbul), await Ids("İstanbul"));
            Assert.Equal(Of(urfa), await Ids("Şanlıurfa"));
            Assert.Equal(Of(afyon), await Ids("afyonkarahisar"));
            Assert.Equal(Of(eregli), await Ids("TR-42-KONYA-EREGLI"));
            Assert.Empty(await Ids("kon"));                                      // anchored: no part of a name
            Assert.Empty(await Ids("onya"));
            foreach (var hostile in new[] { ".*", "kon.*", "konya|istanbul", "TR-42-.*", "[k]onya", "((((" })
            {
                Assert.Empty(await Ids(hostile));                                // escaped: the input never steers the regex
            }

            Assert.DoesNotContain(foreign.Id, await Ids("konya"));               // another tenant's institution
            Assert.DoesNotContain(gone.Id, await Ids("konya"));                  // a deleted one
        }
        finally
        {
            await client.DropDatabaseAsync(name);
        }
    }

    // ═══ helpers ═════════════════════════════════════════════════════════════════════════════════════════

    private static VisitQrService Service(IAccountRepository accounts)
    {
        var tenant = new TenantContext();
        tenant.SetTenant(Tenant);
        return new VisitQrService(tenant, new NullActorContext(), accounts, new FakeInstitutionQrCodeRepository(), new FakeInstitutionQrEventRepository(),
            TestQrKeys.Active(), new Clock(Now));
    }

    private sealed class Clock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }

    /// <summary>Accounts in memory; <see cref="IAccountRepository.ListIdsByCityAsync"/> is the interface DEFAULT (the rule).</summary>
    private sealed class CityAccounts : IAccountRepository
    {
        public List<Account> Items { get; } = new();

        public Account Add(string name, string? city, Guid? tenant = null)
        {
            var a = new Account { Id = Guid.NewGuid(), TenantId = tenant ?? Tenant, AccountName = name, AccountType = "hospital", CityRef = city };
            Items.Add(a);
            return a;
        }

        public Task<IReadOnlyList<Account>> ListByIdsAsync(Guid tenantId, IReadOnlyCollection<Guid> ids, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<Account>>(Items.Where(a => a.TenantId == tenantId && ids.Contains(a.Id)).ToList());

        public Task<Account?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(a => a.TenantId == tenantId && a.Id == id));

        public Task<(IReadOnlyList<Account> Items, long Total, long UnfilteredTotal)> ListAsync(
            Guid tenantId, string? search, int page, int pageSize, string? sortBy, string? sortDir,
            IReadOnlyCollection<string>? statuses, IReadOnlyCollection<string>? accountTypes, IReadOnlyCollection<Guid>? accountIdScope, CancellationToken ct)
            => ListAsync(tenantId, search, page, pageSize, sortBy, sortDir, statuses, accountTypes, accountIdScope, null, ct);

        public Task<(IReadOnlyList<Account> Items, long Total, long UnfilteredTotal)> ListAsync(
            Guid tenantId, string? search, int page, int pageSize, string? sortBy, string? sortDir,
            IReadOnlyCollection<string>? statuses, IReadOnlyCollection<string>? accountTypes, IReadOnlyCollection<Guid>? accountIdScope,
            IReadOnlyCollection<Guid>? excludedAccountIds, CancellationToken ct)
        {
            var all = Items.Where(a => a.TenantId == tenantId && !a.IsDeleted).ToList();
            var rows = all.Where(a => accountIdScope is null || accountIdScope.Contains(a.Id))
                .Where(a => excludedAccountIds is null || !excludedAccountIds.Contains(a.Id))
                .OrderBy(a => a.AccountName, StringComparer.Ordinal).ToList();
            return Task.FromResult<(IReadOnlyList<Account>, long, long)>((rows.Skip((page - 1) * pageSize).Take(pageSize).ToList(), rows.Count, all.Count));
        }

        public Task<Account?> GetByCodeAsync(Guid tenantId, string accountCode, CancellationToken ct) => throw new NotImplementedException();
        public Task<bool> ExistsByCodeAsync(Guid tenantId, string accountCode, Guid? excludeId, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<Account>> GetChildrenAsync(Guid tenantId, Guid parentId, CancellationToken ct) => throw new NotImplementedException();
        public Task<bool> WouldCreateCycleAsync(Guid tenantId, Guid accountId, Guid candidateParentId, CancellationToken ct) => throw new NotImplementedException();
        public Task InsertAsync(Account account, CancellationToken ct) => throw new NotImplementedException();
        public Task UpdateAsync(Account account, CancellationToken ct) => throw new NotImplementedException();
    }
}
