namespace Diten.InventoryService.Application.Common;

/// <summary>
/// WP-MVP1-FU-CLEAN2 (FU-0173-MOV-ACTORNAME) — the display name written on a NEW movement as <c>InventoryTransaction.CreatedByName</c> at
/// posting time. In a request it is the caller's token name (<c>name</c>, else <c>given_name</c> + <c>family_name</c> — the value the central
/// audit queue records). A background flow that acts for a user (the recall quarantine sweeper, the quarantine intake sweeper) has no token: it
/// hands over the name the initiating command STORED (<see cref="Use"/>), e.g. the recall approver's name kept on the quarantine request. The
/// service never calls Auth for it; no name → null (the read then falls back to the audit queue, else no name).
/// </summary>
public interface IPostingActorName
{
    /// <summary>The name set by <see cref="Use"/>, else the token name; null when neither exists.</summary>
    string? Current { get; }

    /// <summary>A background flow's stored name of the user it acts for (scoped: one sweep item = one scope).</summary>
    void Use(string? name);
}
