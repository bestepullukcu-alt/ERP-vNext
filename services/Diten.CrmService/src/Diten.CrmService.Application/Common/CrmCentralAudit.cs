using Diten.BuildingBlocks.CentralAudit;
using Diten.CrmService.Application.Common.Models;

namespace Diten.CrmService.Application.Common;

/// <summary>
/// CRM-AUD-KN-01 (BL-501, AUD-001 path b) — what a marked CRM write command tells Platform's central audit log through
/// the shared forwarder (<c>Diten.BuildingBlocks.CentralAudit</c>). The command names the entity, never its data: no
/// request body, text, comment or person name is copied into the record (AUD-001 §3).
///
/// <para><b>Category</b> = Platform <c>AuditCategory.Crm</c> (19, owner decision K-AUD-1, Beste 2026-11-02).
/// <b>Source module</b> = the module code Platform's CRM manifest registers
/// (<c>services/Diten.Platform/src/Diten.Platform.Application/Features/Crm/SelfRegistration/CrmManifestProvider.cs</c>,
/// <c>ModuleCode: "CRM"</c>).</para>
///
/// <para><b>Not K2</b> (owner decision K-AUD-2, Beste 2026-11-02): the Knowledge commands are not in the §4.4 K2 class;
/// the record is written right after the command succeeds, and a failed write is logged as an error and shows on
/// <c>/health/central-audit</c> — never silent.</para>
/// </summary>
public static class CrmCentralAudit
{
    public const string Category = "Crm";

    public const string SourceModule = "CRM";

    /// <summary>Create / Copy / NewVersion.</summary>
    public static CentralAuditMetadata Create(string entityType) => new(Category, "Create", entityType, SourceModule);

    /// <summary>Update / Arrange / Add* / Remove* / Bind / Resolve.</summary>
    public static CentralAuditMetadata Update(string entityType) => new(Category, "Update", entityType, SourceModule);

    /// <summary>Submit / Decide / Approve / Withdraw / Release / Retire / Archive.</summary>
    public static CentralAuditMetadata Lifecycle(string entityType) => new(Category, "LifecycleTransition", entityType, SourceModule);

    /// <summary>Render.</summary>
    public static CentralAuditMetadata Execute(string entityType) => new(Category, "Execute", entityType, SourceModule);

    /// <summary>The id inside a SUCCEEDED <see cref="Response{T}"/> envelope, or null (failed envelope, no data, other type).</summary>
    public static Guid? IdFrom<T>(object? response, Func<T, Guid> pick)
        => response is Response<T> { IsSuccessful: true, Data: { } data } && pick(data) is var id && id != Guid.Empty
            ? id
            : null;
}
