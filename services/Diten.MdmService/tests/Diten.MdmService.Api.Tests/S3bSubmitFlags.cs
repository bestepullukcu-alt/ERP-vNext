using System.Text.RegularExpressions;
using Diten.MdmService.Domain.Entities;
using MongoDB.Driver;

namespace Diten.MdmService.Api.Tests;

/// <summary>
/// TEST ADAPTATION — MOD-0290-FU04 S3b (R-04): a draft is sent for approval only with the lot / serial flags of its level
/// set. The identity flows the OLDER HTTP tests drive were written before that gate and never set them; their send
/// helpers OPT IN to this, right before a GSKU / LSKU submit, so they keep testing what they test (the workflow) with the
/// flags a real maker now fills first. Only an EMPTY flag is filled, and the version is not moved.
/// <para>
/// Opted in: FgFix1HttpTests, GskuLifecycleHttpTests (+ its partials), LskuFix1/2/3HttpTests (+ LskuFix4). NEVER in the
/// gate's own tests (StockBehaviourS3bHttpTests send through their own helper — a refused submit must meet unset flags;
/// S3b_The_gate_tests_never_take_the_legacy_flag_fill holds that). Production code has no such fill.
/// </para>
/// </summary>
internal static class S3bSubmitFlags
{
    private static readonly Regex GskuSubmit = new(@"/api/gskus/(?<id>[0-9a-fA-F-]{36})/submit$", RegexOptions.CultureInvariant);
    private static readonly Regex LskuSubmit = new(@"/api/lskus/(?<id>[0-9a-fA-F-]{36})/submit$", RegexOptions.CultureInvariant);

    public static async Task FillForLegacyFlowAsync(MdmHttpHost host, string path)
    {
        if (GskuSubmit.Match(path) is { Success: true } gsku)
            await FillGskuLotAsync(host, Guid.Parse(gsku.Groups["id"].Value));
        else if (LskuSubmit.Match(path) is { Success: true } lsku)
        {
            var id = Guid.Parse(lsku.Groups["id"].Value);
            var lskus = host.Database.GetCollection<Lsku>("mdm_lskus");
            await lskus.UpdateOneAsync(
                Builders<Lsku>.Filter.Eq(x => x.Id, id) & Builders<Lsku>.Filter.Eq(x => x.SerialControlled, null),
                Builders<Lsku>.Update.Set(x => x.SerialControlled, true));
            if (await lskus.Find(x => x.Id == id).FirstOrDefaultAsync() is { } row)
                await FillGskuLotAsync(host, row.GskuId);
        }
    }

    private static Task FillGskuLotAsync(MdmHttpHost host, Guid gskuId) =>
        host.Database.GetCollection<Gsku>("mdm_gskus").UpdateOneAsync(
            Builders<Gsku>.Filter.Eq(x => x.Id, gskuId) & Builders<Gsku>.Filter.Eq(x => x.LotControlled, null),
            Builders<Gsku>.Update.Set(x => x.LotControlled, true));
}
