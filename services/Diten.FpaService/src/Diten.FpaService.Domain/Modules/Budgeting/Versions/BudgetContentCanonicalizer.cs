using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Diten.FpaService.Domain.Modules.Budgeting.Financial;

namespace Diten.FpaService.Domain.Modules.Budgeting.Versions;

public static class BudgetContentCanonicalizer
{
    public static byte[] Write(Guid budgetId,long number,string label,CurrencyReferenceV1 currency,BudgetFiscalCalendarReferenceV1 calendar,UnitSemantics units,FundingBaselineType baseline,DateOnly effectiveFrom,string? notes,IReadOnlyList<BudgetPeriod> periods,IReadOnlyList<BudgetLine> lines,ApprovalOutcomeReference? approval=null)
    {
        var periodOrder=periods.ToDictionary(x=>x.PeriodReference,x=>x.Order,StringComparer.Ordinal);
        using var stream=new MemoryStream();using(var w=new Utf8JsonWriter(stream,new JsonWriterOptions{Encoder=JavaScriptEncoder.UnsafeRelaxedJsonEscaping}))
        {
            w.WriteStartObject();w.WriteString("ContractName","fpa.budget-content-canonical-json.v1");w.WriteString("ContractVersion","1.0");w.WriteString("BudgetId",budgetId.ToString("D"));w.WriteNumber("BudgetVersionNumber",number);w.WriteString("RevisionLabel",label.Normalize(NormalizationForm.FormC));
            w.WritePropertyName("CurrencyReference");w.WriteStartObject();w.WriteString("ContractName",currency.ContractName);w.WriteString("ContractVersion",currency.ContractVersion);w.WriteString("AlphabeticCode",currency.AlphabeticCode);w.WriteEndObject();
            w.WritePropertyName("BudgetFiscalCalendarReference");w.WriteStartObject();w.WriteString("ContractName",calendar.ContractName);w.WriteString("ContractVersion",calendar.ContractVersion);w.WriteString("BudgetFiscalCalendarId",calendar.BudgetFiscalCalendarId.ToString("D"));w.WriteEndObject();
            w.WriteString("UnitSemantics",units.ToString());w.WritePropertyName("RoundingPolicy");w.WriteStartObject();w.WriteNumber("Scale",4);w.WriteString("Midpoint","ToEven");w.WriteEndObject();w.WriteString("FundingBaselineType",baseline.ToString());w.WriteString("EffectiveFrom",effectiveFrom.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture));w.WriteString("Notes",(notes??string.Empty).Normalize(NormalizationForm.FormC));
            if(approval is null)w.WriteNull("ApprovalOutcomeReference");else{w.WritePropertyName("ApprovalOutcomeReference");w.WriteStartObject();w.WriteString("ContractName",approval.ContractName);w.WriteString("ContractVersion",approval.ContractVersion);w.WriteString("ApprovalOutcomeId",approval.ApprovalOutcomeId.ToString("D"));w.WriteEndObject();}
            w.WritePropertyName("Periods");w.WriteStartArray();foreach(var p in periods.OrderBy(x=>x.Order)){w.WriteStartObject();w.WriteString("PeriodReference",p.PeriodReference);w.WriteNumber("Order",p.Order);w.WriteString("LocalStartDate",p.LocalStartDate.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture));w.WriteString("LocalEndDateExclusive",p.LocalEndDateExclusive.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture));w.WriteEndObject();}w.WriteEndArray();
            w.WritePropertyName("Lines");w.WriteStartArray();foreach(var l in lines.OrderBy(x=>periodOrder[x.PeriodReference]).ThenBy(x=>x.LineKey,StringComparer.Ordinal).ThenBy(x=>x.LineType)){w.WriteStartObject();w.WriteString("PeriodReference",l.PeriodReference);w.WriteString("LineKey",l.LineKey.Normalize(NormalizationForm.FormC));w.WriteString("LineType",l.LineType.ToString());w.WriteString("Amount",l.Amount.Canonical);w.WriteEndObject();}w.WriteEndArray();w.WriteEndObject();
        }return stream.ToArray();
    }
    public static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
