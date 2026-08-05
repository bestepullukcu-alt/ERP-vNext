using System.Globalization;
using Diten.FpaService.Domain.Modules.Budgeting.Common;

namespace Diten.FpaService.Domain.Modules.Budgeting.Financial;

public enum UnitSemantics { Units, Thousands, Millions }
public enum FundingBaselineType { Original, Reforecast, Contingency }
public enum LineType { Base, Adjustment, Credit }

public sealed record CurrencyReferenceV1
{
    public const string ExpectedContractName = "fpa.currency-reference"; public const string ExpectedContractVersion = "1.0";
    public CurrencyReferenceV1(string contractName, string contractVersion, string alphabeticCode)
    {
        if (contractName != ExpectedContractName || contractVersion != ExpectedContractVersion || alphabeticCode.Length != 3 || alphabeticCode.Any(c => c is < 'A' or > 'Z')) throw new BudgetingDomainException("budgeting.currency.invalid");
        ContractName=contractName; ContractVersion=contractVersion; AlphabeticCode=alphabeticCode;
    }
    public string ContractName { get; } public string ContractVersion { get; } public string AlphabeticCode { get; }
}

public sealed record BudgetFiscalCalendarReferenceV1
{
    public const string ExpectedContractName = "fpa.budget-fiscal-calendar-reference"; public const string ExpectedContractVersion = "1.0";
    public BudgetFiscalCalendarReferenceV1(string contractName, string contractVersion, Guid id)
    {
        if (contractName != ExpectedContractName || contractVersion != ExpectedContractVersion) throw new BudgetingDomainException("budgeting.calendar-reference.invalid");
        ContractName=contractName; ContractVersion=contractVersion; BudgetFiscalCalendarId=BudgetingGuard.Required(id,"budgeting.calendar-reference.id-required");
    }
    public string ContractName { get; } public string ContractVersion { get; } public Guid BudgetFiscalCalendarId { get; }
}

public sealed record RoundingPolicyV1 { private RoundingPolicyV1(){} public static RoundingPolicyV1 Instance { get; }=new(); public int Scale=>4; public string Midpoint=>"ToEven"; }

public readonly record struct BudgetAmount
{
    public const decimal LineAbsoluteLimit=1_000_000_000_000_000.0000m;
    public const decimal VersionAggregateAbsoluteLimit=1_000_000_000_000_000_000.0000m;
    public BudgetAmount(decimal value)
    {
        var scale=(decimal.GetBits(value)[3]>>16)&0x7F;
        if(scale>4||Precision(value)>28||Math.Abs(value)>LineAbsoluteLimit) throw new BudgetingDomainException("budgeting.amount.invalid-precision-scale-or-bound"); Value=value;
    }
    public decimal Value{get;} public string Canonical=>Value.ToString("0.0000",CultureInfo.InvariantCulture);
    public static decimal NormalizeDerived(decimal value)=>decimal.Round(value,4,MidpointRounding.ToEven);
    public static decimal CheckedAggregate(IEnumerable<BudgetAmount> amounts)
    {
        decimal total=0;try{foreach(var amount in amounts)total=checked(total+amount.Value);}catch(OverflowException){throw new BudgetingDomainException("budgeting.amount.aggregate-overflow");}
        total=NormalizeDerived(total);if(Math.Abs(total)>VersionAggregateAbsoluteLimit)throw new BudgetingDomainException("budgeting.amount.aggregate-bound");return total;
    }
    private static int Precision(decimal value){var text=Math.Abs(value).ToString(CultureInfo.InvariantCulture).Replace(".",string.Empty).TrimStart('0');return Math.Max(1,text.Length);}
}

public sealed record BudgetFiscalCalendarPeriodDefinition(string PeriodReference,int Order,DateOnly LocalStartDate,DateOnly LocalEndDateExclusive);
public sealed class BudgetFiscalCalendar
{
    public BudgetFiscalCalendar(BudgetFiscalCalendarReferenceV1 reference,string timeZoneId,int startMonth,IReadOnlyList<BudgetFiscalCalendarPeriodDefinition> periods)
    {
        try{_=TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);}catch(TimeZoneNotFoundException){throw new BudgetingDomainException("budgeting.calendar.timezone-invalid");}
        if(startMonth is<1 or>12||periods.Count!=12||periods[0].LocalStartDate.Month!=startMonth||periods.Select(x=>x.PeriodReference).Distinct(StringComparer.Ordinal).Count()!=12) throw new BudgetingDomainException("budgeting.calendar.invalid");
        for(var i=0;i<periods.Count;i++){var p=periods[i];if(string.IsNullOrWhiteSpace(p.PeriodReference)||p.Order!=i+1||p.LocalEndDateExclusive!=p.LocalStartDate.AddMonths(1)||(i>0&&periods[i-1].LocalEndDateExclusive!=p.LocalStartDate))throw new BudgetingDomainException("budgeting.calendar.periods-invalid");}
        Reference=reference;TimeZoneId=timeZoneId;FiscalYearStartMonth=startMonth;Periods=Array.AsReadOnly(periods.ToArray());
    }
    public BudgetFiscalCalendarReferenceV1 Reference{get;}public string TimeZoneId{get;} public int FiscalYearStartMonth{get;} public IReadOnlyList<BudgetFiscalCalendarPeriodDefinition> Periods{get;}
}
