namespace Diten.Web.Views.Organization.FieldDefinitions;

/// <summary>
/// MOD-0288-FU04 — resource anchor for the Organization field-definition authoring surface.
///
/// <para>Its own type, deliberately: the Task authoring screens have
/// <c>Views.Tasks.FieldDefinitions.TaskFieldDefinitionsIndex</c> and the two never share a resource file.
/// FU02 §7 drew that boundary for the backend; sharing the anchor here would put both modules' strings in one
/// file and make the first divergent label a change to the other module's screens.</para>
/// </summary>
public sealed class OrganizationFieldDefinitionsIndex
{
}
