using Diten.BuildingBlocks.Auditing;
using Diten.CrmService.Application.Common.Models;

namespace Diten.CrmService.Application.Features.Account;

/// <summary>
/// WP-CRM-MOBILE-REQ CRM-ACC-EDIT — MOD-0149 §19 machine codes. The error envelope is <c>errors = [message, code]</c>
/// (one pair per problem; a 400 from the validators lists every pair): the message is an English developer text, the
/// screen words the CODE (Web resx, mobile its own strings). Create and Update use the same codes.
/// </summary>
public static class AccountErrors
{
    // 400 — fields (validators, MOD-0048 reference sets)
    public const string NameRequired = "account_name_required";
    public const string NameTooLong = "account_name_too_long";
    public const string CodeInvalid = "account_code_invalid";
    public const string TypeRequired = "account_type_required";
    public const string TypeInvalid = "account_type_invalid";
    public const string StatusRequired = "account_status_required";
    public const string StatusInvalid = "account_status_invalid";
    public const string CategoryInvalid = "account_category_invalid";
    public const string ReferenceSetMissing = "account_reference_set_missing";
    public const string AddressTooLong = "account_address_too_long";
    public const string LatitudeInvalid = "account_latitude_invalid";
    public const string LongitudeInvalid = "account_longitude_invalid";
    public const string ResponsibleNameTooLong = "account_responsible_name_too_long";
    public const string PhoneInvalid = "account_phone_invalid";
    public const string EmailInvalid = "account_email_invalid";
    public const string NotesTooLong = "account_notes_too_long";
    public const string LogoInvalid = "account_logo_invalid";
    public const string LogoTooLarge = "account_logo_too_large";
    public const string LogoConflict = "account_logo_conflict";
    public const string ExternalIdRequired = "external_reference_id_required";
    public const string ExpectedVersionRequired = "account_expected_version_required";
    public const string TenantRequired = "account_tenant_required";

    // 404
    public const string NotFound = "account_not_found";
    public const string ParentNotFound = "account_parent_not_found";

    // 409
    public const string CodeTaken = "account_code_taken";
    public const string ExternalReferenceTaken = "external_reference_taken";
    public const string VersionConflict = "account_version_conflict";

    // 500 (controlled, §19 retry exhausted)
    public const string CodeGenerationFailed = "account_code_generation_failed";

    public static Response<T> Fail<T>(string message, string code, int status) => Response<T>.Fail(new[] { message, code }, status);

    public static Response<T> Fail<T>(IReadOnlyList<(string Message, string Code)> problems, int status)
        => Response<T>.Fail(problems.SelectMany(p => new[] { p.Message, p.Code }).ToList(), status);
}

/// <summary>The AUD-001 descriptor of the account writes this work made audited (F8-a path b; Create + Update).</summary>
public static class AccountAudit
{
    public const string SourceModule = "MOD-0149";
    public const string Entity = "Account";

    public static AuditDescriptor Describe(AuditOperation operation, Guid? id)
        => new(AuditCategory.Crm, operation, Entity, SourceModule, id is { } value && value != Guid.Empty ? value : null);
}
