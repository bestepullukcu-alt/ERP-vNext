using Diten.SupplyChainService.Application.Features.Shipments;
using Diten.SupplyChainService.Application.Features.Shipments.Commands;
using Diten.SupplyChainService.Application.Features.Shipments.Validators;
using Xunit;

namespace Diten.SupplyChainService.Tests;

public sealed class ShipmentValidatorContractTests
{
    [Theory]
    [InlineData("30.000", true)]
    [InlineData("-30.000", true)]
    [InlineData("0", true)]
    [InlineData("000.010", true)]
    [InlineData("٣٠.٠٠٠", false)]
    [InlineData("۳۰.۰۰۰", false)]
    [InlineData("３０.０００", false)]
    [InlineData("3٠.000", false)]
    [InlineData("1e3", false)]
    [InlineData("+30", false)]
    [InlineData(".5", false)]
    [InlineData("30.", false)]
    public void QuantityMatchesFrozenAsciiDecimal(string quantity, bool expected)
    {
        var line = new ShipmentModels.Line("1", Guid.NewGuid(), Guid.NewGuid(), quantity, "EA", null);
        Assert.Equal(expected, new ShipmentLineValidator().Validate(line).IsValid);
    }

    public static IEnumerable<object?[]> NoteCases()
    {
        yield return [null, true];
        yield return ["", true];
        foreach (var unit in new[] { "a", "😀", "e\u0301", "😀a" })
        {
            var count = unit is "e\u0301" or "😀a" ? 500 : 1000;
            var boundary = string.Concat(Enumerable.Repeat(unit, count));
            yield return [boundary, true];
            yield return [boundary + "😀", false];
        }
    }

    [Theory]
    [MemberData(nameof(NoteCases))]
    public void TransitionNoteUsesUnicodeCodePoints(string? note, bool expected)
    {
        var body = new ShipmentModels.Transition("Planned", DateTimeOffset.Parse("2026-09-20T08:15:00Z"), null, note);
        Assert.Equal(expected, new TransitionShipmentValidator().Validate(new TransitionShipmentCommand(Guid.NewGuid(), body)).IsValid);
    }

    [Theory]
    [MemberData(nameof(NoteCases))]
    public void PodNoteUsesUnicodeCodePoints(string? note, bool expected)
    {
        var body = new ShipmentModels.Pod("Recipient", DateTimeOffset.Parse("2026-09-21T15:42:00Z"), ["test-evidence"], note);
        Assert.Equal(expected, new CapturePodValidator().Validate(new CapturePodCommand(Guid.NewGuid(), body)).IsValid);
    }
}
