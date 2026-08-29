using System.Globalization;
using Diten.CrmService.Domain.Repositories;

namespace Diten.CrmService.Application.Features.Campaign;

/// <summary>MOD-0165 FU10 — generates a CampaignCode when the author leaves the field empty.</summary>
public interface ICampaignCodeGenerator
{
    Task<string> GenerateAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the code <see cref="GenerateAsync"/> would hand out right now, WITHOUT consuming a sequence number.
    /// Null when no free candidate could be found within the retry budget — a hint that cannot be trusted is not shown.
    /// </summary>
    Task<CampaignCodePeek?> PeekAsync(Guid tenantId, CancellationToken cancellationToken);
}

/// <summary>
/// MOD-0165 FU10 — what the next generated code would be. <b>Indicative, never reserved:</b> nothing is written when
/// this is produced, so a concurrent create can take the number before the peeking author saves. That is harmless
/// precisely because the peek is only ever shown, never posted — the field stays empty and the server assigns.
/// </summary>
public sealed record CampaignCodePeek(string CampaignCode, int Year, long Sequence);

/// <summary>
/// MOD-0165 FU10 — CampaignCode generator: <c>CMP-{YYYY}-{sequence:000000}</c>, sequence scoped to tenant + year.
///
/// <para><b>Generation happens at WRITE time, never when a form is opened.</b> Previewing a code on the create screen
/// would burn a sequence number every time someone opened the form and walked away, leaving permanent gaps that look
/// like lost records. The author still sees an editable field and may type their own code; leaving it empty is what
/// asks for one.</para>
///
/// <para><b>Peeking is not generating.</b> <see cref="PeekAsync"/> answers the same question read-only — it reads the
/// counter instead of incrementing it — so the create form can SHOW the code it is about to receive without taking
/// it. The shown value is a placeholder, the field is still submitted empty, and the assignment still happens at
/// save. Two authors peeking at once therefore see the same number and still save under different ones.</para>
///
/// <para>On collision — a manually entered code took the slot — it retries with the next value, and when the retry
/// budget is exhausted it raises a controlled exception. There is no silent fallback: a campaign must never be saved
/// under a code nobody chose.</para>
///
/// <para>The shape mirrors the account code generator deliberately, down to the retry budget, so the two read as one
/// pattern rather than two conventions.</para>
/// </summary>
public sealed class CampaignCodeGenerator : ICampaignCodeGenerator
{
    private const int MaxRetries = 5;

    private readonly ICampaignCodeSequenceRepository _sequences;
    private readonly ICampaignRepository _campaigns;
    private readonly Func<DateTimeOffset> _clock;

    public CampaignCodeGenerator(ICampaignCodeSequenceRepository sequences, ICampaignRepository campaigns)
        : this(sequences, campaigns, () => DateTimeOffset.UtcNow)
    {
    }

    /// <summary>Test seam for a deterministic year.</summary>
    public CampaignCodeGenerator(
        ICampaignCodeSequenceRepository sequences, ICampaignRepository campaigns, Func<DateTimeOffset> clock)
    {
        _sequences = sequences;
        _campaigns = campaigns;
        _clock = clock;
    }

    public async Task<string> GenerateAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var year = _clock().Year;

        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            var next = await _sequences.NextAsync(tenantId, year, cancellationToken);
            var candidate = Format(year, next);

            if (await _campaigns.GetActiveByCodeAsync(tenantId, candidate, cancellationToken) is null)
            {
                return candidate;
            }
        }

        throw new CampaignCodeGenerationException(
            $"Unable to generate a unique CampaignCode for tenant {tenantId} in {year} after {MaxRetries} attempts.");
    }

    public async Task<CampaignCodePeek?> PeekAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var year = _clock().Year;

        // The counter is READ, not incremented, and the collision skip is then walked in memory — the same walk
        // GenerateAsync makes, minus the writes. Nothing here creates the sequence document.
        var next = await _sequences.PeekNextAsync(tenantId, year, cancellationToken);

        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            var candidate = Format(year, next + attempt);

            if (await _campaigns.GetActiveByCodeAsync(tenantId, candidate, cancellationToken) is null)
            {
                return new CampaignCodePeek(candidate, year, next + attempt);
            }
        }

        // Same budget as generation, different consequence: a create still succeeds, it just opens without a hint.
        return null;
    }

    public static string Format(int year, long sequence)
        => string.Create(CultureInfo.InvariantCulture, $"CMP-{year:0000}-{sequence:000000}");
}

/// <summary>Raised when the retry budget is exhausted. Surfaced to the caller rather than swallowed.</summary>
public sealed class CampaignCodeGenerationException : Exception
{
    public CampaignCodeGenerationException(string message) : base(message)
    {
    }
}
