namespace Diten.Platform.Application.Features.WorkAggregation.Services;

/// <summary>
/// Every hour of every day is working time. Saturday counts. 03:00 counts. Public holidays count.
///
/// <para><b>This is deliberate, not unfinished.</b> The name says so on purpose: the next reader must not
/// "fix" it by quietly folding weekend logic in here, and must not delete it as a placeholder. It is the
/// HONEST implementation of "we do not have a working calendar yet" — it makes no claim it cannot keep, and
/// every answer it gives is exactly reproducible.</para>
///
/// <para>The alternative — guessing Mon–Fri 09:00–17:00 — would be worse than this, not better: it would be
/// wrong for every tenant that does not work those hours, wrong in a way nobody could see, and it would make
/// the real calendar's arrival a BEHAVIOUR CHANGE rather than the addition it should be. A tenant reading
/// "2 days left" from this class is reading a plain elapsed-time answer, which is at least the answer they
/// would compute themselves.</para>
///
/// <para>When the working calendar lands (BL: Calendar), it arrives as a second implementation of
/// <see cref="IWorkingTimeCalculator"/> and one DI registration changes. Nothing that CONSUMES working time
/// needs to be touched, because nothing that consumes it does the arithmetic — that is what the seam bought.</para>
/// </summary>
public sealed class TwentyFourSevenWorkingTimeCalculator : IWorkingTimeCalculator
{
    public decimal UnitsBetween(DateTimeOffset from, DateTimeOffset to)
        => (decimal)(to - from).TotalDays;

    public DateTimeOffset Add(DateTimeOffset from, decimal units)
        => from.AddDays((double)units);
}
