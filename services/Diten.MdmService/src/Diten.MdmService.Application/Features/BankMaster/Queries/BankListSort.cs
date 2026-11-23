using Diten.MdmService.Domain.Repositories;

namespace Diten.MdmService.Application.Features.BankMaster.Queries;

/// <summary>The sortable columns of the bank list (the query's <c>sortBy</c>); anything else is a 400.</summary>
public static class BankListSort
{
    public static readonly IReadOnlyList<string> Names = ["code", "name", "country", "status", "createdAt"];

    public static bool TryParse(string? value, out BankListOrder order)
    {
        order = BankListOrder.Code;
        if (string.IsNullOrWhiteSpace(value)) return true;
        switch (value.Trim().ToLowerInvariant())
        {
            case "code": order = BankListOrder.Code; return true;
            case "name": order = BankListOrder.Name; return true;
            case "country": order = BankListOrder.CountryCode; return true;
            case "status": order = BankListOrder.Status; return true;
            case "createdat": order = BankListOrder.CreatedAt; return true;
            default: return false;
        }
    }

    public static bool TryParseDirection(string? value, out bool descending)
    {
        descending = false;
        if (string.IsNullOrWhiteSpace(value)) return true;
        switch (value.Trim().ToLowerInvariant())
        {
            case "asc": return true;
            case "desc": descending = true; return true;
            default: return false;
        }
    }
}
