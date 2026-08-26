using System.Text.Json;
using System.Text.RegularExpressions;
using Diten.Platform.API.Middleware;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Behaviors;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Validators;
using Diten.Platform.Domain.Enums.Tasks;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.Middleware;

/// <summary>
/// BL-040 — a FluentValidation failure reaches the wire carrying a STABLE reason code.
///
/// <para><b>The defect, and why four months of green tests never saw it.</b>
/// <c>ValidationBehavior.TryCreateFailureResponse</c> looked up <c>Response&lt;T&gt;.Fail</c> with a two-type
/// signature; the real one takes four parameters, and optional parameters do not make <c>GetMethod</c>'s type
/// array match. So the lookup returned null on EVERY request, the behaviour threw, and
/// <see cref="GlobalExceptionHandler"/> rendered a <c>ValidationProblemDetails</c> with the right status and no
/// reason code. Every existing test asked "did it answer 400?" — it always did. Nobody asked "does it carry a
/// code?", which is the only question that could have failed.</para>
///
/// <para><b>So every assertion in this file is about the CODE.</b> A 400 assertion here would be green today and
/// would prove nothing.</para>
///
/// <para><b>Why the code matters.</b> The reason-code bridge turns a stable code into a sentence in the reader's
/// own language through the frontend resx (seven languages). A failure with no code arrives as untranslatable
/// English server text — it passes every l10n gate we have and still shows English on screen.</para>
/// </summary>
public sealed class ValidationReasonCodeTests
{
    // ── The derivation: stable, and text-independent ─────────────────────────

    [Fact]
    public void A_rule_with_no_curated_code_still_gets_one_derived_from_the_field_and_the_rule()
    {
        /*
         * The load-bearing choice. Requiring a hand-written code on every rule would mean 150 validators had to
         * be edited before ANY error carried a code — so the platform-wide defect would stay open until the last
         * module was done. Deriving one means every validator that exists today already answers with a code, and
         * a rule that wants a curated one says so.
         */
        var code = ValidationReasonCode.From(Failure("Request.Title", "MaximumLengthValidator"));

        Assert.Equal("VALIDATION_REQUEST_TITLE_MAXIMUM_LENGTH", code);
    }

    [Fact]
    public void The_code_does_NOT_change_when_the_MESSAGE_changes()
    {
        /*
         * The stated criterion, asserted directly: the resx mapping hangs off this code, so an editor improving
         * an English sentence must not silently unmap every translation of it.
         */
        var before = ValidationReasonCode.From(Failure("Request.Title", "NotEmptyValidator", "Title is required."));
        var after = ValidationReasonCode.From(Failure("Request.Title", "NotEmptyValidator", "Please enter a title."));

        Assert.Equal(before, after);
    }

    [Fact]
    public void A_curated_code_is_used_VERBATIM()
    {
        /*
         * `.WithErrorCode("REVIEW_REVIEWER_REQUIRED")` is how a rule opts out of the derived name. It must not be
         * mangled or prefixed: these codes are already mapped in the frontend bridge, and a rule moved from a
         * handler into a validator has to keep answering with the identical string.
         */
        var code = ValidationReasonCode.From(
            Failure("Request.ReviewerCandidateUserId", TaskReasonCodes.ReviewerRequired));

        Assert.Equal(TaskReasonCodes.ReviewerRequired, code);
    }

    [Fact]
    public void A_field_less_failure_still_produces_a_code()
    {
        // Rule-level failures (RuleFor on the model, custom rules) carry an empty property name. Falling back to
        // nothing here would leave exactly the cross-field rules — the interesting ones — code-less.
        var code = ValidationReasonCode.From(Failure(string.Empty, "PredicateValidator"));

        Assert.Equal("VALIDATION_PREDICATE", code);
    }

    [Fact]
    public void Two_different_rules_on_the_SAME_field_get_different_codes()
    {
        /*
         * Non-vacuity for the derivation, and a product requirement: "title is missing" and "title is too long"
         * are different sentences in every language. Collapsing them to one code would make the bridge unable to
         * tell the reader which one happened.
         */
        var empty = ValidationReasonCode.From(Failure("Request.Title", "NotEmptyValidator"));
        var tooLong = ValidationReasonCode.From(Failure("Request.Title", "MaximumLengthValidator"));

        Assert.NotEqual(empty, tooLong);
    }

    // ── The wire: the code actually arrives ──────────────────────────────────

    [Fact]
    public async Task A_REAL_validator_failure_arrives_carrying_a_reason_code()
    {
        /*
         * THE test for this item, and it runs the real parts end to end: the production
         * <see cref="CreateTaskItemValidator"/>, the production <see cref="ValidationBehavior{TRequest,TResponse}"/>,
         * and the production <see cref="GlobalExceptionHandler"/> that writes the body.
         *
         * The command below has no title, which is the validator's very first rule.
         */
        var (status, body) = await ThroughPipelineAsync(CreateCommand(title: string.Empty));

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.True(
            body.TryGetProperty("reason_code", out var code),
            "A validation failure reached the wire with no reason code — the bridge can only show English.");
        Assert.Equal("VALIDATION_REQUEST_TITLE_NOT_EMPTY", code.GetString());
    }

    [Fact]
    public async Task The_field_name_is_reason_code_EXACTLY_because_the_frontend_bridge_reads_that_name()
    {
        // Same pin the blocked-transition path already carries: renaming it to reasonCode would silently fall
        // back to the generic message in the browser.
        var (_, body) = await ThroughPipelineAsync(CreateCommand(title: string.Empty));

        Assert.True(body.TryGetProperty("reason_code", out _));
        Assert.False(body.TryGetProperty("reasonCode", out _));
    }

    [Fact]
    public async Task The_code_describes_the_SAME_failure_the_detail_sentence_describes()
    {
        /*
         * With several failures at once, `detail` shows the first message and `reason_code` must be the first
         * failure's code. If the two were picked independently, the screen would show one sentence localized
         * from a code about a different field — worse than the English it replaced.
         *
         * This command breaks the title AND omits the due date, so there is genuinely more than one.
         */
        var (_, body) = await ThroughPipelineAsync(CreateCommand(title: string.Empty, dueAt: null));

        var code = body.GetProperty("reason_code").GetString();
        var detail = body.GetProperty("detail").GetString();

        Assert.Equal("VALIDATION_REQUEST_TITLE_NOT_EMPTY", code);
        Assert.Equal("Title is required.", detail);
    }

    [Fact]
    public async Task The_existing_400_SHAPE_survives_untouched()
    {
        /*
         * The regression guard, and the reason this fix is additive rather than a shape change. Six client files
         * read `problem.detail` off validation failures (personalization-client, login, reset-password,
         * Administrators, AuditLog, …). Swapping the body for a Response<T> envelope — which is what "make the
         * reflection work" would have done — takes `detail` and the per-field `errors` map away from all of them.
         */
        var (status, body) = await ThroughPipelineAsync(CreateCommand(title: string.Empty));

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.Equal("Validation Error", body.GetProperty("title").GetString());
        Assert.True(body.TryGetProperty("detail", out _));
        Assert.Equal(400, body.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task The_per_field_errors_map_does_NOT_reach_the_wire_and_that_is_a_SEPARATE_defect()
    {
        /*
         * Measured while verifying the fix above, and pinned here as TODAY'S TRUTH rather than quietly assumed.
         *
         * GlobalExceptionHandler builds a ValidationProblemDetails with a per-field errors dictionary, but the
         * switch expression types the result as ProblemDetails and WriteAsJsonAsync serializes by the STATIC
         * type — so the derived type's Errors property is dropped on the way out. (`reason_code` survives only
         * because Extensions is [JsonExtensionData] on the base type.) Every validation 400 this platform has
         * ever sent carried title/status/detail and nothing per-field.
         *
         * NOT fixed in this slice, deliberately: it is a serialization defect in a shared error path, not the
         * reason-code bridge, and widening every ProblemDetails on the wire deserves its own round and its own
         * regression measurement. Registered separately. This test exists so the next reader learns it from a
         * failing assertion rather than from a browser.
         */
        var (_, body) = await ThroughPipelineAsync(CreateCommand(title: string.Empty, dueAt: null));

        Assert.False(body.TryGetProperty("errors", out _));
        // …and the reason code is unaffected, which is why the bridge works regardless.
        Assert.Equal("VALIDATION_REQUEST_TITLE_NOT_EMPTY", body.GetProperty("reason_code").GetString());
    }

    [Fact]
    public async Task A_VALID_command_reaches_the_handler()
    {
        /*
         * NON-VACUITY for the whole wire block. A behaviour that rejected everything would satisfy every
         * assertion above while making the platform unusable.
         */
        var reached = false;
        await new ValidationBehavior<CreateTaskItemCommand, Response<Guid>>([new CreateTaskItemValidator()])
            .Handle(
                CreateCommand(title: "Geçerli iş"),
                () => { reached = true; return Task.FromResult(Response<Guid>.Success(Guid.NewGuid())); },
                CancellationToken.None);

        Assert.True(reached);
    }

    // ── The mechanism that produced the defect is gone ───────────────────────

    [Fact]
    public void The_behaviour_no_longer_looks_a_factory_up_by_REFLECTION()
    {
        /*
         * The defect was not the wrong type array — it was that a reflective lookup can fail SILENTLY. It
         * returned null on every request for four months and nothing anywhere said so.
         *
         * A "corrected" signature would keep that failure mode alive: the next parameter added to
         * Response&lt;T&gt;.Fail would break the match again, just as quietly. So the lookup is gone entirely,
         * and this asserts its absence rather than trusting a comment to keep it away.
         */
        // CODE only. The doc comment above the class names GetMethod on purpose — explaining what was removed and
        // why is the point of it — so scanning the raw file would fail on its own explanation.
        var code = StripComments(File.ReadAllText(BehaviourSourcePath()));

        Assert.DoesNotContain("GetMethod", code, StringComparison.Ordinal);
        Assert.DoesNotContain(".Invoke(", code, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Reflection", code, StringComparison.Ordinal);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Drops line, block and XML-doc comments, so prose about the ban does not read as a violation.</summary>
    private static string StripComments(string source)
    {
        var withoutBlocks = Regex.Replace(source, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        return Regex.Replace(withoutBlocks, @"//.*?$", string.Empty, RegexOptions.Multiline);
    }

    private static ValidationFailure Failure(string property, string errorCode, string message = "whatever")
        => new(property, message) { ErrorCode = errorCode };

    private static CreateTaskItemCommand CreateCommand(string title, DateTimeOffset? dueAt = null)
        => new(
            new CreateTaskItemRequest(
                Title: title,
                Description: null,
                Priority: TaskPriority.Medium,
                AssignmentTarget: TaskAssignmentTarget.SelfAssigned,
                AssigneeUserId: null,
                PoolPositionId: null,
                OrganizationUnitId: Guid.Parse("44444444-4444-4444-4444-444444444444"),
                DueAt: dueAt ?? DateTimeOffset.UtcNow.AddDays(3),
                StartAt: null,
                PlannedDate: null,
                EstimateHours: null,
                Tags: null,
                ReviewRequired: false,
                ApprovalRequired: false,
                ApprovalManagerUserId: null,
                EmailNotificationsEnabled: false,
                DelegationAllowed: false,
                FieldValues: null,
                Watchers: null,
                ReviewerCandidateUserId: null),
            "corr-validation");

    /// <summary>
    /// Runs the command through the production behaviour and, when it refuses, through the production exception
    /// handler — so what is asserted is the body the browser receives, not an intermediate object.
    /// </summary>
    private static async Task<(int Status, JsonElement Body)> ThroughPipelineAsync(CreateTaskItemCommand command)
    {
        var thrown = await Assert.ThrowsAsync<ValidationException>(() =>
            new ValidationBehavior<CreateTaskItemCommand, Response<Guid>>([new CreateTaskItemValidator()])
                .Handle(
                    command,
                    () => Task.FromResult(Response<Guid>.Success(Guid.NewGuid())),
                    CancellationToken.None));

        var context = new DefaultHttpContext();
        var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        await new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance)
            .TryHandleAsync(context, thrown, CancellationToken.None);

        responseBody.Seek(0, SeekOrigin.Begin);
        using var document = JsonDocument.Parse(responseBody);
        return (context.Response.StatusCode, document.RootElement.Clone());
    }

    private static string BehaviourSourcePath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "services")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine(
            directory!.FullName,
            "services", "Diten.Platform", "src", "Diten.Platform.Application",
            "Contracts", "Behaviors", "ValidationBehavior.cs");
    }
}
