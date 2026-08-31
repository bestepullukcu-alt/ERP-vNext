using System.Text;
using Diten.AuthService.Domain.S2S;

namespace Diten.AuthService.Application.Tests.S2S;

public sealed class CanonicalRequestBindingTests
{
    [Fact]
    public void Hash_is_deterministic_and_binds_every_input_without_normalization()
    {
        var tenant = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var body = Encoding.UTF8.GetBytes("{\"value\":1}");
        var first = CanonicalRequestBinding.Compute("POST", "/internal/decisions", tenant, "decision.create", body);

        Assert.Equal(first, CanonicalRequestBinding.Compute("POST", "/internal/decisions", tenant, "decision.create", body));
        Assert.NotEqual(first, CanonicalRequestBinding.Compute("PUT", "/internal/decisions", tenant, "decision.create", body));
        Assert.NotEqual(first, CanonicalRequestBinding.Compute("POST", "/internal/decisions/", tenant, "decision.create", body));
        Assert.NotEqual(first, CanonicalRequestBinding.Compute("POST", "/internal/decisions", Guid.NewGuid(), "decision.create", body));
        Assert.NotEqual(first, CanonicalRequestBinding.Compute("POST", "/internal/decisions", tenant, "decision.update", body));
        Assert.NotEqual(first, CanonicalRequestBinding.Compute("POST", "/internal/decisions", tenant, "decision.create", Encoding.UTF8.GetBytes("{\"value\":2}")));
    }

    [Theory]
    [InlineData("post", "/internal/decisions", "decision.create")]
    [InlineData("POST", "internal/decisions", "decision.create")]
    [InlineData("POST", "/internal/decisions?tenant=x", "decision.create")]
    [InlineData("POST", "/internal/decisions", "Decision.Create")]
    [InlineData("POST", "/internal/decisions", "decision.*")]
    public void Noncanonical_input_is_rejected(string method, string path, string operation)
    {
        Assert.Throws<S2SContractException>(() =>
            CanonicalRequestBinding.Compute(method, path, Guid.NewGuid(), operation, "{}"u8));
    }
}
