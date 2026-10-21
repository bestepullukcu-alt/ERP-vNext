using System.Text.RegularExpressions;

namespace Diten.Web.Controllers.CRM;

/// <summary>
/// WP-CRM-MOBILE-REQ CRM-ACC-EDIT — reads the CRM account error envelope (<c>errors = [message, code, …]</c>, MOD-0149 §19)
/// into (form field, code, message) triples: a coded problem lands under its field (409 code / external reference, 400
/// field rules) and is worded from the page resx by its code; an uncoded message (older envelope) stays a summary line.
/// </summary>
public static partial class AccountGatewayErrors
{
    public const string VersionConflict = "account_version_conflict";

    [GeneratedRegex("^[a-z][a-z0-9]*(_[a-z0-9]+)+$")]
    private static partial Regex MachineCode();

    public sealed record Problem(string? Field, string? Code, string Message);

    public static IReadOnlyList<Problem> Parse(IReadOnlyList<string> errors)
    {
        var problems = new List<Problem>();
        for (var i = 0; i < errors.Count; i++)
        {
            var message = errors[i];
            if (i + 1 < errors.Count && MachineCode().IsMatch(errors[i + 1]))
            {
                var code = errors[++i];
                problems.Add(new Problem(FieldOf(code), code, message));
            }
            else
            {
                problems.Add(new Problem(null, null, message));
            }
        }

        return problems;
    }

    /// <summary>The form field a code belongs to (null = the summary).</summary>
    public static string? FieldOf(string code) => code switch
    {
        _ when code.StartsWith("account_name_", StringComparison.Ordinal) => "AccountName",
        "account_code_taken" or "account_code_invalid" => "AccountCode",
        _ when code.StartsWith("account_type_", StringComparison.Ordinal) => "AccountType",
        _ when code.StartsWith("account_status_", StringComparison.Ordinal) => "Status",
        "account_category_invalid" => "AccountCategory",
        "account_address_too_long" => "AddressLine",
        "account_latitude_invalid" => "Latitude",
        "account_longitude_invalid" => "Longitude",
        "account_responsible_name_too_long" => "ResponsiblePersonName",
        "account_phone_invalid" => "ResponsiblePersonPhone",
        "account_email_invalid" => "ResponsiblePersonEmail",
        "account_notes_too_long" => "Notes",
        _ when code.StartsWith("account_logo_", StringComparison.Ordinal) => "LogoDataUri",
        _ when code.StartsWith("external_reference_", StringComparison.Ordinal) => "ExternalReference",
        _ => null
    };

    /// <summary>The page resx key that words a code ("Error.account_code_taken"); the controller falls back to the
    /// server message when the key is absent.</summary>
    public static string ResourceKey(string code) => "Error." + code;
}
