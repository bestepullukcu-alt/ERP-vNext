using System.Net;
using System.Reflection;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Handlers.QueryHandlers;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Clusters;
using MongoDB.Driver.Core.Connections;
using MongoDB.Driver.Core.Servers;
using Xunit;

namespace Diten.MdmService.Application.Tests;

/// <summary>WP-MDM-ABB-ACCEPT-01 FIX2 — rules measured without a database.</summary>
public sealed class ProductAbbreviationFix2UnitTests
{
    private static readonly ConnectionId Connection =
        new(new ServerId(new ClusterId(), new DnsEndPoint("localhost", 27017)));

    [Fact]
    public void A_server_that_cannot_run_transactions_is_recognised_whichever_way_it_says_so()
    {
        // FIX3 item 6 — the driver refuses before sending (NotSupportedException) when it already knows the server is
        // standalone; otherwise the server refuses the first statement (IllegalOperation, code 20). Neither is a write
        // conflict or an outage. (FIX2's label-only race classification is gone: FIX3 item 5 lets the stored state
        // decide, measured against Mongo in ProductAbbreviationFix1MongoTests.)
        var refusedByServer = new MongoCommandException(Connection, "illegal", new BsonDocument("insert", 1),
            new BsonDocument
            {
                ["ok"] = 0, ["code"] = 20, ["codeName"] = "IllegalOperation",
                ["errmsg"] = "Transaction numbers are only allowed on a replica set member or mongos"
            });
        var writeConflict = new MongoCommandException(Connection, "WriteConflict", new BsonDocument("commitTransaction", 1),
            new BsonDocument { ["ok"] = 0, ["code"] = 112, ["codeName"] = "WriteConflict" });

        Assert.True(ProductAbbreviationRegisterRepository.TransactionsAreUnsupported(
            new NotSupportedException("Standalone servers do not support transactions.")));
        Assert.True(ProductAbbreviationRegisterRepository.TransactionsAreUnsupported(refusedByServer));
        Assert.False(ProductAbbreviationRegisterRepository.TransactionsAreUnsupported(writeConflict));
        Assert.False(ProductAbbreviationRegisterRepository.TransactionsAreUnsupported(new TimeoutException()));
        // FIX4 item 7 — only the driver's own standalone refusal; any other NotSupportedException is a bug, not an outage.
        Assert.False(ProductAbbreviationRegisterRepository.TransactionsAreUnsupported(
            new NotSupportedException("Decisions are not supported by this repository.")));
    }

    [Theory]
    [InlineData("""{ "setName": "rs0", "isWritablePrimary": true }""", true)]
    [InlineData("""{ "msg": "isdbgrid" }""", true)]
    [InlineData("""{ "isWritablePrimary": true }""", false)]
    [InlineData("""{ "setName": "" }""", false)]
    public void The_startup_check_reads_transaction_support_from_hello(string hello, bool supported)
        => Assert.Equal(supported,
            Diten.MdmService.Api.Services.ProductAbbreviationRegister.ProductAbbreviationTransactionSupportCheck
                .SupportsTransactions(BsonDocument.Parse(hello)));

    [Theory]
    [InlineData("abı")]
    [InlineData("ſab")]
    public async Task The_scope_guard_resolves_an_abbreviation_only_through_the_registers_own_grammar(string value)
    {
        // FIX2 item 6 — the guard used its own Trim().ToUpperInvariant(): "abı" became ABI and was looked up.
        var looked = new List<string>();
        var register = Proxy<IProductAbbreviationRegisterRepository>((method, args) =>
        {
            if (method.Name == nameof(IProductAbbreviationRegisterRepository.ResolveActiveAsync))
            {
                looked.Add((string)args![0]!);
                return Task.FromResult<ProductAbbreviationRegisterEntry?>(null);
            }

            throw new InvalidOperationException(method.Name);
        });
        var tenant = new TenantContext();
        tenant.SetTenant(Guid.NewGuid());
        var rollout = Proxy<IProductLegalEntityScopeRolloutStateRepository>((method, _) => method.Name switch
        {
            nameof(IProductLegalEntityScopeRolloutStateRepository.GetAsync)
                => Task.FromResult<ProductLegalEntityScopeRolloutState?>(null),
            _ => throw new InvalidOperationException(method.Name)
        });
        var guard = new ProductAbbreviationScopeGuard(
            register,
            Proxy<IGlobalProductRepository>((method, _) => throw new InvalidOperationException(method.Name)),
            rollout,
            Proxy<IProductLegalEntityScopePolicyRepository>((method, _) => throw new InvalidOperationException(method.Name)),
            new ProductLegalEntityScopeCandidateFacade(
                Proxy<Diten.MdmService.Application.Contracts.Authorization.ITrustedLegalEntityScopeProvider>((m, _) => throw new InvalidOperationException(m.Name)),
                Proxy<ILegalEntityRepository>((m, _) => throw new InvalidOperationException(m.Name)),
                tenant,
                new Actor()),
            tenant);

        var result = await guard.EvaluateAbbreviationAsync(value, "mdm.product-abbreviations.read", CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Empty(looked);
        await guard.EvaluateAbbreviationAsync(" abi ", "mdm.product-abbreviations.read", CancellationToken.None);
        Assert.Equal(["ABI"], looked);
    }

    private static T Proxy<T>(Func<MethodInfo, object?[]?, object?> implementation) where T : class
    {
        var value = DispatchProxy.Create<T, StubProxy>();
        ((StubProxy)(object)value).Implementation = implementation;
        return value;
    }

    public class StubProxy : DispatchProxy
    {
        public Func<MethodInfo, object?[]?, object?> Implementation { get; set; } = null!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Implementation(targetMethod!, args);
    }

    private sealed record Actor : Diten.MdmService.Application.Contracts.IProductIdentityActorContext
    {
        public string ActorId { get; } = Guid.NewGuid().ToString("D");
    }
}
