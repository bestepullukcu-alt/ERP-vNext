package eu.grandmedical.diten.mobile.core.database

import androidx.room.Database
import androidx.room.RoomDatabase
import androidx.room.TypeConverters
import eu.grandmedical.diten.mobile.core.database.converter.DitenTypeConverters
import eu.grandmedical.diten.mobile.core.database.dao.CachedReadinessDao
import eu.grandmedical.diten.mobile.core.database.entity.CachedReadinessEntity

/**
 * The single Room database for the app — the offline Single Source Of Truth.
 *
 * `exportSchema = true` writes each version's schema to `schemas/` (see the
 * `room.schemaLocation` KSP arg) so migrations can be tested against real
 * historical schemas. Bump [DB_VERSION] and add a `Migration` to
 * [eu.grandmedical.diten.mobile.core.database.migration.DitenMigrations] for
 * every schema change.
 */
@Database(
    entities = [CachedReadinessEntity::class],
    version = DitenDatabase.DB_VERSION,
    exportSchema = true,
)
@TypeConverters(DitenTypeConverters::class)
abstract class DitenDatabase : RoomDatabase() {

    abstract fun cachedReadinessDao(): CachedReadinessDao

    companion object {
        /** Current schema version. Increment on every entity/schema change. */
        const val DB_VERSION = 1

        /** On-disk database file name. */
        const val DB_NAME = "diten.db"
    }
}
