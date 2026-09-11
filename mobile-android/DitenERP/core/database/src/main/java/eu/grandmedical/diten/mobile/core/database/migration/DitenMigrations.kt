package eu.grandmedical.diten.mobile.core.database.migration

import androidx.room.migration.Migration
import androidx.sqlite.db.SupportSQLiteDatabase

/**
 * Central registry of Room migrations, applied in order by the database builder.
 *
 * The database is at version 1, so there are no migrations yet and [ALL] is
 * empty. We deliberately do **not** call `fallbackToDestructiveMigration()` in
 * production: destructive fallback silently wipes the user's offline cache on a
 * missing/failed migration, which for a partitioned SSOT means data loss across
 * every tenant and legal entity. Instead, each schema bump must ship an explicit
 * `Migration` here; a genuinely missing migration should fail loudly in tests
 * (via `MigrationTestHelper`) rather than nuke data on-device.
 *
 * Adding the next migration follows this template:
 *
 * ```
 * val MIGRATION_1_2 = object : Migration(1, 2) {
 *     override fun migrate(db: SupportSQLiteDatabase) {
 *         db.execSQL(
 *             "ALTER TABLE cached_readiness ADD COLUMN note TEXT NOT NULL DEFAULT ''",
 *         )
 *     }
 * }
 * ```
 *
 * then bump `DitenDatabase.DB_VERSION` to 2 and add `MIGRATION_1_2` to [ALL].
 */
object DitenMigrations {

    /** All migrations, oldest-first. Empty at v1. */
    val ALL: Array<Migration> = emptyArray()

    /**
     * Reference implementation kept private so it is compiled and stays honest,
     * but not yet wired into [ALL] (there is no version 2). Copy this shape when
     * the first real schema change lands.
     */
    @Suppress("unused")
    private val exampleMigration1To2 = object : Migration(1, 2) {
        override fun migrate(db: SupportSQLiteDatabase) {
            // Example only — no-op template for the first real migration.
        }
    }
}
