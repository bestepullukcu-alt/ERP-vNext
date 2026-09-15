using Diten.Web.Services;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// BL-398 (WP-PSS-MOD0024-FOLLOWUPS-02) — the reader itself, extracted from
/// <c>TaskFieldDefinitionsController</c> (where <c>TaskFieldDefinitionGatewayRoundTripTests</c> already covers
/// it end to end) into a shared helper five Task-screen controllers now call. This file measures the RULE in
/// isolation; the per-controller wiring is <see cref="GatewayProblemDetailsReaderControllerTests"/>.
/// </summary>
public sealed class GatewayProblemDetailsReaderTests
{
    private const string SortOrderConversionMessage =
        "The JSON value could not be converted to System.Int32. Path: $.sortOrder | LineNumber: 0 | BytePositionInLine: 171.";

    private const string ModelBindingProblemDetails =
        "{\"type\":\"https://tools.ietf.org/html/rfc9110#section-15.5.1\",\"title\":\"One or more validation errors occurred.\"," +
        "\"status\":400,\"errors\":{\"request\":[\"The request field is required.\"],\"$.sortOrder\":[\"" + SortOrderConversionMessage + "\"]}," +
        "\"traceId\":\"00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-00\"}";

    [Fact]
    public void A_field_binding_ProblemDetails_yields_its_distinct_field_messages()
    {
        var recognised = GatewayProblemDetailsReader.TryReadErrors(ModelBindingProblemDetails, out var errors);

        Assert.True(recognised);
        Assert.Equal(["The request field is required.", SortOrderConversionMessage], errors);
    }

    [Fact]
    public void A_ProblemDetails_with_no_field_messages_is_still_recognised_with_an_empty_list()
    {
        // The generic-message DECISION is the caller's (each controller keeps its own localized GatewayError
        // key) — this helper only reports that the shape WAS a ProblemDetails.
        const string body = "{\"type\":\"https://tools.ietf.org/html/rfc9110#section-15.5.1\",\"title\":\"Bad Request\",\"status\":400}";

        var recognised = GatewayProblemDetailsReader.TryReadErrors(body, out var errors);

        Assert.True(recognised);
        Assert.Empty(errors);
    }

    [Fact]
    public void The_Platform_envelope_shape_is_not_a_ProblemDetails()
    {
        // No "errors" object (it is an ARRAY on the envelope) and no title+status pair.
        const string body = "{\"data\":null,\"isSuccessful\":false,\"statusCode\":409,\"errors\":[\"Code already exists.\"]}";

        Assert.False(GatewayProblemDetailsReader.TryReadErrors(body, out var errors));
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json at all")]
    [InlineData("[1,2,3]")]
    public void Anything_that_is_not_a_ProblemDetails_object_is_refused_not_thrown(string raw)
    {
        Assert.False(GatewayProblemDetailsReader.TryReadErrors(raw, out var errors));
        Assert.Empty(errors);
    }

    [Fact]
    public void Duplicate_field_messages_across_fields_are_collapsed_once()
    {
        const string body = "{\"title\":\"x\",\"status\":400,\"errors\":{\"a\":[\"Same.\"],\"b\":[\"Same.\"]}}";

        GatewayProblemDetailsReader.TryReadErrors(body, out var errors);

        Assert.Equal(["Same."], errors);
    }

    // ⚠ SABOTAGE GUARD — the one property that makes this helper safe to share across five controllers with no
    // localizer of its own: it never invents text. Removing the empty-check above (making the caller's fallback
    // unreachable) would surface here as an empty string sneaking into the list.
    [Fact]
    public void Blank_or_whitespace_only_field_messages_are_dropped_not_kept_as_empty_strings()
    {
        const string body = "{\"title\":\"x\",\"status\":400,\"errors\":{\"a\":[\"\",\"   \",\"Real message.\"]}}";

        GatewayProblemDetailsReader.TryReadErrors(body, out var errors);

        Assert.Equal(["Real message."], errors);
    }
}
