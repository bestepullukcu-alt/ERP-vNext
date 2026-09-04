using System.Text.Json.Serialization;

namespace Diten.Platform.Application.Features.DocumentManagementMasterRegister.Models;

// DCP-005 (document-management side, Phase 1) — controlled-document EFFECTIVENESS read contract.
//
// This is the single application-layer vocabulary the effectiveness resolver (ResolveDocumentEffectivenessQuery /
// Handler) and its in-process gate (IControlledDocumentEffectivenessPort) both speak. It reads the live Document
// Master Register (MOD-0029-FU06) so a consumer — the Task Center (MOD-0024) task-type activation gate — no longer
// looks at a retired CSV copy. Phase 1 resolves identifiers to a state at THIS instant; continuous surveillance
// (a document falling out of force after activation) is explicitly out of scope (contract §6, backlog).

/// <summary>
/// DCP-005 — the disjoint outcome of resolving one identifier against the Document Master Register. The three states
/// carry different meanings and MUST stay distinct (contract §2):
/// <list type="bullet">
/// <item><see cref="Effective"/> — in force (LifecycleStatus ∈ {Effective, UnderRevision}).</item>
/// <item><see cref="Blocked"/> — recorded in the register but NOT in force (any other lifecycle status).</item>
/// <item><see cref="Unresolved"/> — no register row resolves to the identifier (a DATA fact: "no such document").</item>
/// </list>
/// <see cref="Unresolved"/> is never used for an infrastructure failure — a register read that throws propagates as a
/// thrown exception ("could not check"), never as an <see cref="Unresolved"/> result (contract §2/§5, fail-closed).
///
/// This state crosses the HTTP boundary (the effectiveness:batch response `state` field), so it carries the per-enum
/// [JsonConverter(typeof(JsonStringEnumConverter))] the service convention requires (Enums/Tasks/TaskEnums.cs:7-11,
/// Enums/EntitlementSource.cs). Without it System.Text.Json writes the numeric value on the wire — unloggable, and it
/// silently mis-reads once a member is inserted. Applied per-enum, never via a global AddJsonOptions change.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DocumentEffectivenessState
{
    Effective,
    Blocked,
    Unresolved
}

/// <summary>
/// DCP-005 — which register identity field an identifier is matched against. No silent default: the caller states
/// <see cref="By"/> explicitly (contract §1); the join key itself is fixed by the Step-0 register-seed decision, not
/// by a parameter default.
/// </summary>
public enum DocumentIdentifierKind
{
    Code,
    Uid
}

/// <summary>
/// DCP-005 — the resolved effectiveness of one requested identifier. <see cref="BlockedReason"/> is the register's own
/// word (the LifecycleStatus name) when <see cref="State"/> is <see cref="DocumentEffectivenessState.Blocked"/>, and is
/// null otherwise. <see cref="DocumentCode"/> / <see cref="PermanentUid"/> / <see cref="LifecycleStatus"/> are echoed
/// from the resolved row and are null when the identifier is <see cref="DocumentEffectivenessState.Unresolved"/>.
/// </summary>
public sealed record DocumentEffectivenessItem(
    string Identifier,
    DocumentEffectivenessState State,
    string? DocumentCode,
    string? PermanentUid,
    string? LifecycleStatus,
    string? BlockedReason);

/// <summary>DCP-005 — the batch resolution result: one <see cref="DocumentEffectivenessItem"/> per resolved identifier.</summary>
public sealed record DocumentEffectivenessResult(IReadOnlyList<DocumentEffectivenessItem> Items);
