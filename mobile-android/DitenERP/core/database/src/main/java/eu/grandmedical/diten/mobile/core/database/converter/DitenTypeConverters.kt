package eu.grandmedical.diten.mobile.core.database.converter

import androidx.room.TypeConverter
import eu.grandmedical.diten.mobile.core.database.entity.SyncStatus
import java.time.Instant

/**
 * Shared Room type converters.
 *
 * Kept deliberately dependency-free (no JSON library) so `:core:database` stays
 * self-contained. Handles:
 *  - [Instant] <-> epoch millis, for any future timestamp columns,
 *  - `List<String>` <-> a delimited string, for tag/label columns,
 *  - [SyncStatus] <-> its stable enum name.
 *
 * Registered on [eu.grandmedical.diten.mobile.core.database.DitenDatabase] via
 * `@TypeConverters`.
 */
class DitenTypeConverters {

    // --- Instant <-> epoch millis ---------------------------------------------

    @TypeConverter
    fun instantToEpochMillis(value: Instant?): Long? = value?.toEpochMilli()

    @TypeConverter
    fun epochMillisToInstant(value: Long?): Instant? = value?.let(Instant::ofEpochMilli)

    // --- List<String> <-> delimited string ------------------------------------
    //
    // A unit separator (U+001F) is used as the delimiter because it cannot appear
    // in normal display text, so no escaping is required. An empty list encodes to
    // an empty string; a single blank element is preserved.

    @TypeConverter
    fun stringListToString(value: List<String>?): String? =
        value?.joinToString(LIST_DELIMITER)

    @TypeConverter
    fun stringToStringList(value: String?): List<String>? =
        when {
            value == null -> null
            value.isEmpty() -> emptyList()
            else -> value.split(LIST_DELIMITER)
        }

    // --- SyncStatus <-> name ---------------------------------------------------

    @TypeConverter
    fun syncStatusToName(value: SyncStatus?): String? = value?.name

    @TypeConverter
    fun nameToSyncStatus(value: String?): SyncStatus? = value?.let(SyncStatus::valueOf)

    private companion object {
        const val LIST_DELIMITER = "\u001F"
    }
}
