using Diten.AuthService.Application.S2S;
using Diten.AuthService.Domain.S2S;

namespace Diten.AuthService.Application.Tests.S2S;

public sealed class ReplayReceiptContractTests
{
    [Fact]
    public void Receipt_retains_uniqueness_metadata_through_expiry_plus_skew()
    {
        var accepted = DateTimeOffset.UtcNow;
        var expiry = accepted.AddMinutes(5);
        var receipt = new S2SReplayReceipt("diten-auth-service", Guid.NewGuid().ToString("D"),
            Guid.NewGuid().ToString("D"), "request-hash", expiry, accepted);

        Assert.Equal(expiry.AddSeconds(30), receipt.RetainUntilUtc);
        Assert.Equal(401, ReplayReceiptAcceptance.Replay().SuggestedHttpStatusCode);
        Assert.Equal(503, ReplayReceiptAcceptance.AuthorityUnavailable().SuggestedHttpStatusCode);
    }
}
