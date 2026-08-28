using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.WorkingCalendar.Queries;

/// <summary>
/// Read-only working-day question. <paramref name="Operation"/> selects which of the five provider methods runs:
/// <c>is-working-day</c> | <c>is-holiday</c> | <c>next-working-day</c> | <c>add-working-days</c> |
/// <c>working-days-between</c>. Never writes.
/// </summary>
public sealed record ResolveWorkingDayQuery(
    string Operation,
    DateOnly Date,
    string CountryCode,
    Guid? OrganizationUnitId = null,
    Guid? LegalEntityId = null,
    DateOnly? ToDate = null,
    int? Days = null) : IRequest<Response<WorkingDayResolveDto>>;

public static class WorkingCalendarOperations
{
    public const string IsWorkingDay = "is-working-day";
    public const string IsHoliday = "is-holiday";
    public const string NextWorkingDay = "next-working-day";
    public const string AddWorkingDays = "add-working-days";
    public const string WorkingDaysBetween = "working-days-between";

    public static readonly IReadOnlyList<string> All =
        new[] { IsWorkingDay, IsHoliday, NextWorkingDay, AddWorkingDays, WorkingDaysBetween };

    public static bool IsValid(string? value) => value is not null && All.Contains(value, StringComparer.Ordinal);
}
