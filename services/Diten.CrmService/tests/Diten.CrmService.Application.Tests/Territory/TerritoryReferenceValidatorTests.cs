using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.Territory;
using Xunit;

namespace Diten.CrmService.Application.Tests.Territory;

public sealed class TerritoryReferenceValidatorTests
{
    private static readonly IReadOnlyDictionary<string, string> RankTen =
        new Dictionary<string, string> { ["rank"] = "10", ["sortOrder"] = "10" };

    // ---- ReferenceMetadata string parsing (MOD-0048 attributes come as strings, never native JSON) ----

    [Fact]
    public void Metadata_Int_Parses_String_Number()
    {
        Assert.True(ReferenceMetadata.TryGetInt(RankTen, "rank", out var rank));
        Assert.Equal(10, rank);
    }

    [Fact]
    public void Metadata_Bool_Parses_String_Boolean()
    {
        var attrs = new Dictionary<string, string> { ["requiresTerritoryId"] = "true", ["allowsBusinessScope"] = "false" };
        Assert.True(ReferenceMetadata.TryGetBool(attrs, "requiresTerritoryId", out var required));
        Assert.True(required);
        Assert.True(ReferenceMetadata.TryGetBool(attrs, "allowsBusinessScope", out var allows));
        Assert.False(allows);
    }

    [Fact]
    public void Metadata_Missing_Key_Fails_Closed()
    {
        Assert.False(ReferenceMetadata.TryGetInt(RankTen, "notThere", out _));
        Assert.False(ReferenceMetadata.TryGetBool(null, "anything", out _));
    }

    [Fact]
    public void Metadata_Invalid_Parse_Fails_Closed()
    {
        var attrs = new Dictionary<string, string> { ["rank"] = "abc", ["flag"] = "notabool" };
        Assert.False(ReferenceMetadata.TryGetInt(attrs, "rank", out _));
        Assert.False(ReferenceMetadata.TryGetBool(attrs, "flag", out _));
    }

    // ---- ResolveLevelRankAsync fail-closed ----

    private static TerritoryReferenceValidator Build(FakeReferenceValidator v, FakeMetadataReader m, FakeCatalogReader? c = null)
        => new(v, m, c ?? new FakeCatalogReader());

    [Fact]
    public async Task ResolveLevelRank_Valid_Value_With_Rank_Metadata_Succeeds()
    {
        var v = new FakeReferenceValidator().Publish(TerritoryReferenceSets.TerritoryLevel, "zone");
        var m = new FakeMetadataReader().Set(TerritoryReferenceSets.TerritoryLevel, "zone", RankTen);

        var result = await Build(v, m).ResolveLevelRankAsync("zone", default);

        Assert.True(result.Ok);
        Assert.Equal(10, result.Rank);
    }

    [Fact]
    public async Task ResolveLevelRank_Unpublished_Set_Is_SetMissing_And_No_Default_Rank()
    {
        var v = new FakeReferenceValidator().MarkMissing(TerritoryReferenceSets.TerritoryLevel);
        var result = await Build(v, new FakeMetadataReader()).ResolveLevelRankAsync("zone", default);

        Assert.False(result.Ok);
        Assert.Equal(TerritoryReferenceIssue.SetMissing, result.Issue);
        Assert.Equal(0, result.Rank); // never a hardcoded fallback rank
    }

    [Fact]
    public async Task ResolveLevelRank_Unknown_Value_Is_InvalidValue()
    {
        var v = new FakeReferenceValidator().Publish(TerritoryReferenceSets.TerritoryLevel, "zone");
        var result = await Build(v, new FakeMetadataReader()).ResolveLevelRankAsync("not-a-level", default);

        Assert.Equal(TerritoryReferenceIssue.InvalidValue, result.Issue);
    }

    [Fact]
    public async Task ResolveLevelRank_Missing_Rank_Metadata_Fails_Closed()
    {
        var v = new FakeReferenceValidator().Publish(TerritoryReferenceSets.TerritoryLevel, "zone");
        // value valid but no attributes at all
        var m = new FakeMetadataReader().Set(TerritoryReferenceSets.TerritoryLevel, "zone", null);

        var result = await Build(v, m).ResolveLevelRankAsync("zone", default);
        Assert.Equal(TerritoryReferenceIssue.MetadataMissing, result.Issue);
    }

    [Fact]
    public async Task ResolveLevelRank_Unparseable_Rank_Fails_Closed()
    {
        var v = new FakeReferenceValidator().Publish(TerritoryReferenceSets.TerritoryLevel, "zone");
        var m = new FakeMetadataReader().Set(TerritoryReferenceSets.TerritoryLevel, "zone",
            new Dictionary<string, string> { ["rank"] = "abc" });

        var result = await Build(v, m).ResolveLevelRankAsync("zone", default);
        Assert.Equal(TerritoryReferenceIssue.MetadataInvalid, result.Issue);
    }

    // ---- GetReadinessAsync ----

    [Fact]
    public async Task Readiness_Unpublished_Sets_Are_Not_Ready()
    {
        var validator = Build(new FakeReferenceValidator(), new FakeMetadataReader(), new FakeCatalogReader());

        var readiness = await validator.GetReadinessAsync(default);

        Assert.Equal(TerritoryReferenceSets.Required.Count, readiness.Count);
        Assert.All(readiness, r => Assert.False(r.Ready));
        Assert.Contains(readiness, r => r.SetCode == TerritoryReferenceSets.TerritoryLevel && r.ActualValueCount == 0);
    }

    [Fact]
    public async Task Readiness_Published_Set_With_Required_Metadata_Is_Ready()
    {
        var catalog = new FakeCatalogReader().Set(TerritoryReferenceSets.TerritoryModelStatus,
            new ReferenceSetSnapshot(TerritoryReferenceSets.TerritoryModelStatus, true, new[]
            {
                new ReferenceValueSnapshot("draft", "Draft", null, true, false, null)
            }));

        var readiness = await Build(new FakeReferenceValidator(), new FakeMetadataReader(), catalog).GetReadinessAsync(default);

        var modelStatus = readiness.Single(r => r.SetCode == TerritoryReferenceSets.TerritoryModelStatus);
        Assert.True(modelStatus.Ready);
        Assert.Equal(1, modelStatus.ActualValueCount);
    }

    [Fact]
    public async Task Readiness_Level_Missing_Rank_Metadata_Is_Not_MetadataReady()
    {
        // territory-level published with a value that has NO rank metadata → metadataReady=false → not ready.
        var catalog = new FakeCatalogReader().Set(TerritoryReferenceSets.TerritoryLevel,
            new ReferenceSetSnapshot(TerritoryReferenceSets.TerritoryLevel, true, new[]
            {
                new ReferenceValueSnapshot("zone", "Zone", null, true, false, null)
            }));

        var readiness = await Build(new FakeReferenceValidator(), new FakeMetadataReader(), catalog).GetReadinessAsync(default);

        var level = readiness.Single(r => r.SetCode == TerritoryReferenceSets.TerritoryLevel);
        Assert.False(level.MetadataReady);
        Assert.False(level.Ready);
        Assert.Contains("rank", level.MissingMetadata);
    }
}
