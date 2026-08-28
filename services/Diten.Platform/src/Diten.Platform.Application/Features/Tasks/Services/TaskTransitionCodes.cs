using Diten.Platform.Domain.Enums.Tasks;

namespace Diten.Platform.Application.Features.Tasks.Services;

/// <summary>
/// WC-1 — the wire spelling of a lifecycle transition.
///
/// <para><b>Why a map and not <c>kind.ToString()</c>.</b> An enum reaching a client as a number is a defect this
/// module has shipped twice, and reaching it as PascalCase is the same defect wearing a shirt: the contract's
/// vocabularies are lowerCamel (<c>valueType</c>, <c>direction</c>, <c>admissionState</c>), and a name that drifts
/// when someone renames a C# member is a wire break disguised as a refactor. The map is the seam where the two
/// vocabularies are pinned to each other, and <c>TaskTransitionCodeTests</c> fails if a new kind arrives without
/// one.</para>
/// </summary>
public static class TaskTransitionCodes
{
    private static readonly IReadOnlyDictionary<TaskTransitionKind, string> Map =
        new Dictionary<TaskTransitionKind, string>
        {
            [TaskTransitionKind.Created] = "created",
            [TaskTransitionKind.Accepted] = "accepted",
            [TaskTransitionKind.Planned] = "planned",
            [TaskTransitionKind.Started] = "started",
            [TaskTransitionKind.Resumed] = "resumed",
            [TaskTransitionKind.Waiting] = "waiting",
            [TaskTransitionKind.SubmittedForReview] = "submittedForReview",
            [TaskTransitionKind.ReviewCancelled] = "reviewCancelled",
            [TaskTransitionKind.Completed] = "completed",
            [TaskTransitionKind.Cancelled] = "cancelled",
            [TaskTransitionKind.Claimed] = "claimed",
            [TaskTransitionKind.Released] = "released",
            [TaskTransitionKind.Reassigned] = "reassigned",
            [TaskTransitionKind.Returned] = "returned",

            /*
             * The unnamed act still gets a code, and still reaches the screen.
             *
             * Dropping it would be the silent hole again: a transition that happened, was recorded, and then
             * vanished on the way out because nobody had labelled it. The client renders it as "the task changed"
             * — less than the truth, but not a lie, and the row's timestamp and actor are still real.
             */
            // A field edit. Its own code because the sentence it produces is unlike every other one here: the
            // others name an act ("started", "cancelled"), this one names what changed.
            [TaskTransitionKind.Edited] = "edited",

            [TaskTransitionKind.Unknown] = "unknown"
        };

    /// <summary>The code for a kind. Every kind has one — see the test that enumerates the enum to prove it.</summary>
    public static string For(TaskTransitionKind kind) => Map[kind];

    /// <summary>Declared for the tests that walk the whole vocabulary.</summary>
    public static IReadOnlyDictionary<TaskTransitionKind, string> All => Map;
}
