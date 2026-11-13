using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

/// <summary>
/// WP-SYNC-144 BL-640 — the update of a whole record as a field-level <c>$set</c> of exactly the elements this binary's
/// class map writes (every mapped member, <c>_id</c> excepted), instead of a whole-document replace. A field a LATER
/// binary added to the stored document is not one of them, so it survives an older binary's update — the rollback risk
/// of a lenient map (MdmDocumentClassMaps ignores extra elements) written by <c>ReplaceOne</c>.
/// <para>What it does NOT do: the elements it sets are written whole, so an embedded list (a record's prices, history,
/// review rounds…) is rewritten with this binary's element shape — a sub-field a later binary added to an ELEMENT of such
/// a list is still lost on an older binary's update (named in the BL-640 report).</para>
/// </summary>
internal static class FieldLevelUpdate
{
    public static UpdateDefinition<T> SetMappedFields<T>(T record)
    {
        var document = record.ToBsonDocument();
        document.Remove("_id");
        return new BsonDocumentUpdateDefinition<T>(new BsonDocument("$set", document));
    }
}
