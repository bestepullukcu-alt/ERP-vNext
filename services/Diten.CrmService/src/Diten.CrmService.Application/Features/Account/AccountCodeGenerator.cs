using System.Globalization;
using Diten.CrmService.Domain.Repositories;

namespace Diten.CrmService.Application.Features.Account;

/// <summary>
/// AccountCode generator (§10.1a): format ACC-{YYYY}-{sequence:000000}, sequence scoped to tenant + year.
/// On collision (a manually-entered code took the slot) it retries with the next sequence value; when the
/// retry budget is exhausted it raises a controlled <see cref="AccountCodeGenerationException"/> (no silent fallback).
/// </summary>
public sealed class AccountCodeGenerator : IAccountCodeGenerator
{
    private const int MaxRetries = 5;
    private readonly IAccountCodeSequenceRepository _sequences;
    private readonly IAccountRepository _accounts;
    private readonly Func<DateTimeOffset> _clock;

    public AccountCodeGenerator(IAccountCodeSequenceRepository sequences, IAccountRepository accounts)
        : this(sequences, accounts, () => DateTimeOffset.UtcNow)
    {
    }

    // Test seam for deterministic year.
    public AccountCodeGenerator(IAccountCodeSequenceRepository sequences, IAccountRepository accounts, Func<DateTimeOffset> clock)
    {
        _sequences = sequences;
        _accounts = accounts;
        _clock = clock;
    }

    public async Task<string> GenerateAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var year = _clock().Year;

        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            var next = await _sequences.NextAsync(tenantId, year, cancellationToken);
            var candidate = Format(year, next);

            if (!await _accounts.ExistsByCodeAsync(tenantId, candidate, excludeId: null, cancellationToken))
            {
                return candidate;
            }
        }

        throw new AccountCodeGenerationException(
            $"Unable to generate a unique AccountCode for tenant {tenantId} in {year} after {MaxRetries} attempts.");
    }

    public static string Format(int year, long sequence)
        => string.Create(CultureInfo.InvariantCulture, $"ACC-{year:0000}-{sequence:000000}");
}
