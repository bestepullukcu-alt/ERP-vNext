using System.Text;
using Diten.AuthService.Domain.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.AuthService.Persistence.Repositories;

/// <summary>
/// BL-521 FIX8 item 3 — ONE DEFINITION OF "A PLATFORM ACCOUNT" IN MONGO FORM, EQUAL TO <see cref="User.IsPlatformAccount"/>. The
/// entity says an account is a platform account when its <c>PlatformActorType</c> holds at least one non-whitespace character
/// (<c>!string.IsNullOrWhiteSpace</c>). FIX7 asked Mongo the question two other ways: <c>Eq(null)</c> (list, lookup, counters —
/// which HID an account whose value is "" or "  " although the entity says it is not a platform account) and <c>Ne(null)</c>
/// (the role-holder lookup — which COUNTED the same account as one, so a tenant actor's permission removal skipped its sessions).
/// Now the user repository (list, lookup, role-holder ids) and the list reader (list, export, counters) all use these two filters,
/// and the whitespace set is not written by hand: it is built from <see cref="char.IsWhiteSpace(char)"/> itself, the very test
/// <see cref="string.IsNullOrWhiteSpace"/> applies, so the two cannot drift apart (<c>PlatformAccountFilterParityTests</c> compares
/// them over real documents).
/// </summary>
internal static class PlatformAccountFilter
{
    // every character the framework calls whitespace, as the body of a regex character class (none of them is a class
    // metacharacter: ] \ ^ -), built once
    private static readonly string WhitespaceClass = BuildWhitespaceClass();

    /// <summary>The account has a platform actor type: some non-whitespace character in it (a missing or null value never matches).</summary>
    public static readonly FilterDefinition<User> IsPlatformAccount =
        Builders<User>.Filter.Regex(u => u.PlatformActorType, new BsonRegularExpression($"[^{WhitespaceClass}]"));

    /// <summary>The account has none: missing, null, empty or whitespace only — what a tenant actor is allowed to see.</summary>
    public static readonly FilterDefinition<User> IsNotPlatformAccount = Builders<User>.Filter.Or(
        Builders<User>.Filter.Eq(u => u.PlatformActorType, null),
        Builders<User>.Filter.Regex(u => u.PlatformActorType, new BsonRegularExpression($"^[{WhitespaceClass}]*$")));

    private static string BuildWhitespaceClass()
    {
        var builder = new StringBuilder();
        for (var code = 0; code <= char.MaxValue; code++)
        {
            var character = (char)code;
            if (char.IsWhiteSpace(character))
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }
}
