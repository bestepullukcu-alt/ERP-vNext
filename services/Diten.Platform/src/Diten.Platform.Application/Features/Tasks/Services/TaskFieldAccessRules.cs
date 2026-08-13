using Diten.Platform.Application.Contracts;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;

namespace Diten.Platform.Application.Features.Tasks.Services;

/// <summary>
/// BL-024 Phase 2 — the ONE place that decides whether a caller may see or write a configurable field.
///
/// <para><b>Why one place.</b> The decision is needed in four: the Tasks detail response, the Work Center
/// projection, the option/records endpoint and the write validator. Four copies of a security rule is four
/// chances to fix three of them — the exact shape of BL-042 and BL-051, which were both "one fact, several
/// writers, one forgotten". Every caller asks here; nobody re-implements.</para>
///
/// <para><b>Pure.</b> No repository, no HTTP, no cache. It takes the definition and the caller's permission
/// context and returns an answer, which is what lets a definition change take effect on the very next request:
/// there is nothing holding an older one.</para>
/// </summary>
public static class TaskFieldAccessRules
{
    /// <summary>
    /// May this caller SEE the value?
    ///
    /// <para>Three cases, and the third is the one worth reading twice:</para>
    /// <list type="number">
    /// <item>A definition with no <c>ViewPermission</c> — visible. Unrestricted is the state every field written
    /// before this feature carries, so nothing goes dark on deploy.</item>
    /// <item>A definition naming a permission — the caller must hold it.</item>
    /// <item><b>NO definition at all</b> — retired and purged, or written before the catalogue existed. The rule
    /// cannot be read, so it is taken from the VALUE, which carries the classification copied onto it at write
    /// time. <c>Normal</c> stays visible (that is the ordinary case and hiding it would blank real data);
    /// anything above Normal is redacted, because a value that was once marked sensitive must not become
    /// readable by losing its definition. Fail-closed exactly where it matters and nowhere else.</item>
    /// </list>
    /// </summary>
    public static bool CanView(TaskFieldValue value, TaskFieldDefinition? definition, IActorPermissionContext actor)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(actor);

        /*
         * The value's OWN stored flag still suppresses, and it is checked first.
         *
         * It is a different statement from the permission rule — "this particular value is withheld from
         * everyone", not "this caller may not see it" — and it can only ever hide MORE, never reveal. Dropping it
         * when the rule arrived would have been a silent widening: any value an operator had pinned would have
         * started flowing again, and the only sign would have been the data on somebody's screen.
         *
         * Folded in HERE rather than AND-ed at the four call sites, because the moment the answer is assembled in
         * more than one place the places start disagreeing.
         */
        if (value.Redacted)
        {
            return false;
        }

        if (definition is null)
        {
            return value.Classification == TaskFieldClassification.Normal;
        }

        return actor.Has(definition.ViewPermission);
    }

    /// <summary>
    /// May this caller WRITE the field?
    ///
    /// <para><b>Read access is a floor, not a substitute.</b> The two keys are independent — holding
    /// <c>ViewPermission</c> grants nothing about writing, and that is the whole reason they are separate
    /// columns. What the floor rules out is the incoherent case: a caller who may not SEE a value must not be
    /// able to set one, because that is a covert write into a field they cannot verify, and because the client
    /// never received the value and so cannot round-trip it.</para>
    ///
    /// <para>Takes the DEFINITION alone, with no value: a create supplies fields that do not exist yet, so there
    /// is nothing stored to consult. A definition that cannot be found is not writable at all — an unknown code
    /// is already refused upstream, and answering "yes" here for one would be a second way in.</para>
    /// </summary>
    public static bool CanEdit(TaskFieldDefinition? definition, IActorPermissionContext actor)
    {
        ArgumentNullException.ThrowIfNull(actor);

        if (definition is null)
        {
            return false;
        }

        return actor.Has(definition.ViewPermission) && actor.Has(definition.EditPermission);
    }

    /// <summary>
    /// May this caller reach the field's OPTION LIST?
    ///
    /// <para>The same answer as <see cref="CanView(TaskFieldValue, TaskFieldDefinition?, IActorPermissionContext)"/>,
    /// deliberately: a hidden field whose picker still answers is a hidden field in name only. BL-024's own note
    /// raised this — the options and records routes sit on <c>platform.tasks.read</c>, so redacting the value
    /// while leaving the selector open would let anyone enumerate the very list the field was hidden to
    /// protect.</para>
    ///
    /// <para>Its own method rather than a call to <c>CanView</c> with a fabricated value, because there is no
    /// value here to fabricate: the caller is asking about a DEFINITION. A definition that cannot be found is
    /// refused, so this route can never become a general "search any module" endpoint.</para>
    /// </summary>
    public static bool CanReadOptions(TaskFieldDefinition? definition, IActorPermissionContext actor)
    {
        ArgumentNullException.ThrowIfNull(actor);

        return definition is not null && actor.Has(definition.ViewPermission);
    }
}
