using System.Security.Cryptography;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Users.Services;

namespace Diten.AuthService.Application.Tests.Users;

/// <summary>
/// WP-USERS-ROLES-CLOSE-FIX1 FIX2 item 5 — an anonymous request cannot make the link door work: a token that is not one
/// this service issues (64 random bytes, standard Base64 — 88 characters) and an address longer than an address can be are
/// refused BEFORE anything is hashed or read. The well-formed token of the same length goes on to the hash and the read.
/// </summary>
public sealed class TenantPasswordLinkTests
{
    private static readonly string WellFormed = Convert.ToBase64String(RandomNumberGenerator.GetBytes(TenantPasswordLink.TokenBytes));

    public static TheoryData<string?, string> Refused() => new()
    {
        { null, "a@b.test" },
        { "", "a@b.test" },
        { "junk", "a@b.test" },
        { new string('A', 4096), "a@b.test" },                               // long, not the token's length
        { WellFormed[..^4] + "!!==", "a@b.test" },                           // the right length, not Base64
        { Convert.ToBase64String(new byte[65])[..88], "a@b.test" },          // 88 characters that are not 64 bytes
        { WellFormed, new string('a', TenantPasswordLink.MaxEmailLength) + "@b.test" } // an address past the limit
    };

    [Theory]
    [MemberData(nameof(Refused))]
    public async Task A_malformed_link_or_address_is_refused_before_anything_is_hashed_or_read(string? token, string email)
    {
        var hasher = new CountingHasher();

        var (state, user, hash) = await TenantPasswordLink.ReadAsync(new InMemoryUserRepository([]), hasher, email, token, CancellationToken.None);

        Assert.Equal(TenantPasswordLinkState.Invalid, state);
        Assert.Null(user);
        Assert.Null(hash);
        Assert.Equal(0, hasher.Calls);
    }

    [Fact]
    public async Task A_well_formed_token_goes_on_to_the_hash_and_the_read()
    {
        var hasher = new CountingHasher();

        var (state, _, hash) = await TenantPasswordLink.ReadAsync(new InMemoryUserRepository([]), hasher, "a@b.test", WellFormed, CancellationToken.None);

        Assert.Equal(TenantPasswordLinkState.Invalid, state);   // no such account — but it was looked up
        Assert.Equal(1, hasher.Calls);
        Assert.NotNull(hash);
        Assert.True(TenantPasswordLink.IsWellFormedToken(WellFormed));
    }

    private sealed class CountingHasher : IRefreshTokenHasher
    {
        public int Calls { get; private set; }
        public string Hash(string refreshToken) { Calls++; return "h:" + refreshToken; }
    }
}
