using Diten.ProcurementService.Api.Tests.TestInfrastructure;
using Diten.ProcurementService.Domain.Entities;
using Diten.ProcurementService.Persistence.Suppliers;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Diten.ProcurementService.Api.Tests.Suppliers.Migration;

/// <summary>MOD-0140 DL-2: okuma anahtarı (geri alma), vergi no normalizasyonu (K-2) ve test Mongo'sunun port kuralı (§17).</summary>
public sealed class SupplierModelSwitchTests
{
    [Fact]
    public void Without_the_key_the_model_is_the_per_company_one() =>
        Assert.Equal(SupplierModel.PerLegalEntity, SupplierModelSwitch.FromConfiguration(Config(null)).Model);

    [Theory]
    [InlineData("TenantShared", SupplierModel.TenantShared)]
    [InlineData(" tenantshared ", SupplierModel.TenantShared)]
    [InlineData("PerLegalEntity", SupplierModel.PerLegalEntity)]
    public void The_key_selects_the_model(string value, SupplierModel expected) =>
        Assert.Equal(expected, SupplierModelSwitch.FromConfiguration(Config(value)).Model);

    [Theory]
    [InlineData("Shared")]
    [InlineData("1")]
    [InlineData("PerCompany")]
    public void An_unknown_value_stops_startup_instead_of_falling_back(string value) =>
        Assert.Throws<InvalidOperationException>(() => SupplierModelSwitch.FromConfiguration(Config(value)));

    [Theory]
    [InlineData("12 345.678-90", "1234567890")]
    [InlineData("ab\t-cd", "ABCD")]
    [InlineData(" - . ", null)]
    [InlineData(null, null)]
    public void Tax_ids_drop_spaces_dots_and_dashes_and_are_upper_case(string? taxId, string? expected) =>
        Assert.Equal(expected, SupplierTaxId.Normalize(taxId));

    [Theory]
    [InlineData(27017, false)]
    [InlineData(27021, false)]
    [InlineData(27022, true)]
    [InlineData(49151, true)]
    [InlineData(49152, false)]
    [InlineData(60000, false)]
    public void The_test_replica_set_stays_out_of_the_live_and_ephemeral_ports(int port, bool allowed) =>
        Assert.Equal(allowed, ProcurementMongoReplicaSet.PortPolicy.IsAllowed(port));

    [Fact]
    public void Port_selection_skips_forbidden_and_busy_candidates()
    {
        var candidates = new Queue<int>([27017, 50000, 30000, 30001]);
        var port = ProcurementMongoReplicaSet.PortPolicy.Select(candidates.Dequeue, p => p == 30000);
        Assert.Equal(30001, port);
    }

    private static IConfiguration Config(string? model) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(model is null ? [] : [new KeyValuePair<string, string?>(SupplierModelSwitch.ConfigurationKey, model)])
            .Build();
}
