using Diten.BuildingBlocks.Auditing;
using Diten.CrmService.Application.Common.Models;

namespace Diten.CrmService.Application.Features.Commercial.PreOrders;

/// <summary>WP-VW-W6 W6-BE-a — MOD-0168 §13 error codes (the first element of <c>Errors</c>; the second is an English
/// developer message, the screen words the code from resx).</summary>
public static class PreOrderErrors
{
    public const string Invalid = "pre_order_request_invalid";
    public const string NotFound = "pre_order_not_found";
    public const string Stale = "pre_order_stale_version";
    public const string PharmacyInvalid = "pre_order_pharmacy_invalid";
    public const string DepotInvalid = "pre_order_depot_invalid";
    public const string DepotRequired = "pre_order_depot_required";
    public const string DepotNoEmail = "pre_order_depot_no_email";
    public const string LinesInvalid = "pre_order_lines_invalid";
    public const string SkuRetired = "pre_order_sku_retired";
    public const string SkuNotFound = "pre_order_sku_not_found";
    public const string MfInvalid = "pre_order_mf_invalid";
    public const string DiscountInvalid = "pre_order_discount_invalid";
    public const string DiscountNotAllowed = "pre_order_discount_not_allowed";
    public const string DeliveryDateInvalid = "pre_order_delivery_date_invalid";
    public const string NoteTooLong = "pre_order_note_too_long";
    public const string SignatureRequired = "pre_order_signature_required";
    public const string SignatureInvalid = "pre_order_signature_invalid";
    public const string HardLimit = "pre_order_hard_limit";
    public const string NoSeat = "pre_order_no_seat";
    public const string ContextUnavailable = "pre_order_context_unavailable";
    public const string NoCompanyPrefix = "pre_order_no_company_prefix";
    public const string NoApprover = "pre_order_no_approver";
    public const string NotEditable = "pre_order_not_editable";
    public const string NotOwner = "pre_order_not_owner";
    public const string AlreadyDecided = "pre_order_already_decided";
    public const string InvalidTransition = "pre_order_invalid_transition";
    public const string Sod = "pre_order_sod";
    public const string NotApprover = "pre_order_not_approver";
    public const string ReasonRequired = "pre_order_reason_required";
    public const string DecisionInvalid = "pre_order_decision_invalid";
    public const string LinkUnusable = "pre_order_link_unusable";
    public const string DepotResponseInvalid = "pre_order_depot_response_invalid";
    public const string CancelForbidden = "pre_order_cancel_forbidden";
    public const string SettingsInvalid = "pre_order_settings_invalid";
    public const string PrefixLocked = "pre_order_prefix_locked";
    public const string WorkflowUnavailable = "pre_order_workflow_unavailable";
    public const string WorkflowTemplateMissing = "pre_order_workflow_template_missing";
    public const string StoreUnavailable = "pre_order_store_unavailable";
}

public static class PreOrderLimits
{
    public const int MaxLines = 50;
    public const int NoteMax = 1000;
    public const int ReasonMax = 500;
    public const int SignerNameMax = 200;
    public const long SignatureMaxBytes = 10L * 1024 * 1024;
    public const int PageSizeMax = 100;
    public const int ExportMax = 5000;
    public const int PharmacyHistoryMonths = 6;
    public const int PharmacyHistoryMax = 200;
}

/// <summary>The AUD-001 descriptor of every MOD-0168 write (F8-a; §20.3 AÇIK-6: audited, not Faz 8 debt).</summary>
public static class PreOrderAudit
{
    public const string SourceModule = "MOD-0168";
    public const string PreOrderEntity = "PreOrder";
    public const string SettingsEntity = "PreOrderSettings";

    public static AuditDescriptor Describe(AuditOperation operation, string entityType, Guid? id)
        => new(AuditCategory.Crm, operation, entityType, SourceModule, id is { } value && value != Guid.Empty ? value : null);
}

internal static class PreOrderResults
{
    public static Response<T> Fail<T>(string code, string message, int status) => Response<T>.Fail(new[] { code, message }, status);

    public static Response<T> NotFound<T>() => Fail<T>(PreOrderErrors.NotFound, "The pre-order does not exist.", 404);

    public static Response<T> Stale<T>() => Fail<T>(PreOrderErrors.Stale, "The pre-order was changed by someone else; reload it.", 409);

    public static Response<T> Invalid<T>(string message) => Fail<T>(PreOrderErrors.Invalid, message, 400);

    public static Response<T> Transition<T>(string status, string action)
        => Fail<T>(PreOrderErrors.InvalidTransition, $"A {status} pre-order cannot be {action}.", 409);

    public static Response<T> ContextUnavailable<T>(string what)
        => Fail<T>(PreOrderErrors.ContextUnavailable, $"{what} could not be asked; try again.", 503);

    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
