namespace Diten.Platform.Application.Features.WorkAggregation.Services;

/// <summary>
/// WC-2 — how much WORKING time lies between two instants, and where a span of working time lands.
///
/// <para><b>Why an interface for arithmetic this small.</b> Because the arithmetic is the part that is wrong. A
/// deadline is not measured in elapsed hours: a task due Monday that arrives Friday afternoon has one working day
/// left, not three. Nothing in this codebase can answer that yet, and the calendar that will (BL: Calendar) is a
/// separate piece of work. Putting the seam in FIRST means that work replaces one class instead of hunting down
/// every place a day count was subtracted inline — which is exactly the state WC-2 found the SLA logic in, in the
/// browser, with a hard-coded threshold.</para>
///
/// <para><b>Why exactly these two questions.</b> They are the two the SLA needs and they are not
/// interchangeable once a real calendar exists:</para>
/// <list type="bullet">
/// <item><see cref="UnitsBetween"/> answers "how much working time is left" — a MEASURE, used to report.</item>
/// <item><see cref="Add"/> answers "when does the warning window open" — a BOUNDARY, used to decide. Deriving the
/// boundary by subtracting from the measure gives the same answer under a 24/7 calendar and a different one under
/// a real one: two working days before a Monday deadline is the preceding Thursday, not the preceding
/// Saturday.</item>
/// </list>
///
/// <para>The unit is a WORKING DAY. What a working day contains is the implementation's business — that is the
/// whole point of the seam — so callers must never convert a unit to hours or to calendar days themselves.</para>
/// </summary>
public interface IWorkingTimeCalculator
{
    /// <summary>
    /// Working days from <paramref name="from"/> to <paramref name="to"/>. SIGNED: negative when
    /// <paramref name="to"/> is in the past, which is how an overdue span is expressed.
    /// </summary>
    decimal UnitsBetween(DateTimeOffset from, DateTimeOffset to);

    /// <summary>
    /// The instant <paramref name="units"/> working days after <paramref name="from"/>. A negative value walks
    /// backwards, which is how a warning window's opening instant is found from a deadline.
    /// </summary>
    DateTimeOffset Add(DateTimeOffset from, decimal units);
}
