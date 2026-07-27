namespace Diten.Web.Controllers;

/// <summary>
/// The ONE canonical list of MOD-0024 transition action codes the proxy will forward.
///
/// <para><b>Why this exists.</b> A projected action code is not a label — the client turns it straight into a URL
/// (<c>Tasks/api.js</c>: <c>POST /Tasks/api/{id}/{code}</c>). So three things must agree: what the Platform
/// provider PUBLISHES, what this proxy ACCEPTS, and what Platform's own controller EXPOSES. When `inquire` was
/// added to the provider and to Platform but not to the proxy's route constraint, the button rendered, the user
/// pressed it, and the proxy answered 404 before the request ever left Diten.Web. That was the third defect of
/// this exact shape in one session (an MVC-reserved route parameter, an enum serialized as a number, and this).</para>
///
/// <para><b>Why the pattern is duplicated as a literal.</b> A route constraint must be a compile-time constant, so
/// <see cref="Pattern"/> cannot be built from <see cref="All"/> and handed to the attribute. The two are therefore
/// kept in step by TESTS rather than by the compiler: TaskTransitionRouteTests asserts that the attribute on
/// <c>TasksController.ApiTransition</c> carries exactly this pattern, that the pattern accepts every code in
/// <see cref="All"/>, and that it rejects anything else. Platform's side is checked from its own suite, which
/// reads this file — the two services share no assembly.</para>
///
/// <para>Adding an action is therefore a THREE-line change: Platform's controller route, the provider's projected
/// code, and this list. Miss one and a test says which.</para>
/// </summary>
public static class TaskTransitionRoutes
{
    /// <summary>
    /// Route-constraint pattern for the <c>transition</c> segment. MUST list exactly <see cref="All"/>.
    /// Anchored so a code is matched whole — an unanchored pattern would forward <c>cancel-everything</c>.
    /// </summary>
    public const string Pattern = "^(accept|claim|release|plan|start|inquire|complete|cancel)$";

    /// <summary>
    /// Every transition code the proxy forwards, in the order they appear in <see cref="Pattern"/>.
    ///
    /// <para><c>inquire</c> parks a task in Waiting; <c>start</c> also serves the "resume" button, because the
    /// resume action is projected with the code <c>start</c> — same endpoint, different label.</para>
    /// </summary>
    public static readonly IReadOnlyList<string> All =
    [
        "accept",
        "claim",
        "release",
        "plan",
        "start",
        "inquire",
        "complete",
        "cancel"
    ];
}
