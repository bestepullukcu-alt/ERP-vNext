using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.TenantOrganization.Commands;

/// <param name="AllowReportingLineChange">
/// MOD-0288-FU02 §14 — whether this caller was authorized to MOVE the unit, as opposed to edit it.
///
/// <para>⚠ DEFAULT FALSE, AND THE DEFAULT IS THE POINT. Re-parenting is a structural decision with its own
/// approval route in the manager's control matrix; renaming is not. Whoever holds
/// <c>platform.organization-units.update</c> keeps everything they could do before EXCEPT silently re-hanging
/// the unit, which now needs <c>platform.organization-units.reporting-line.update</c> and its own endpoint.
/// A line that is sent unchanged is not a change and is never refused, so an editor that round-trips the whole
/// record keeps working.</para>
/// </param>
public sealed record UpdateOrganizationUnitCommand(
    Guid Id,
    OrganizationUnitRequest Request,
    bool AllowReportingLineChange = false)
    : IRequest<Response<NoContent>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        Category: AuditCategory.TenantAdministration, Operation: AuditOperation.Update, EntityType: "OrganizationUnit",
        EntityId: Id, SourceModule: "organization");
}

/// <summary>
/// MOD-0288-FU02 — move a unit on one or both reporting lines, and change nothing else.
///
/// <para>Declared beside <see cref="UpdateOrganizationUnitCommand"/> deliberately: they are two authorizations
/// over one aggregate, and reading them in one place is what keeps the pair from drifting apart.</para>
/// </summary>
public sealed record UpdateOrganizationUnitReportingLinesCommand(
    Guid Id,
    OrganizationUnitReportingLinesRequest Request)
    : IRequest<Response<NoContent>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        Category: AuditCategory.TenantAdministration, Operation: AuditOperation.Update,
        EntityType: "OrganizationUnit", EntityId: Id, SourceModule: "organization");
}
