package eu.grandmedical.diten.mobile.core.database.migration

import androidx.room.testing.MigrationTestHelper
import androidx.test.platform.app.InstrumentationRegistry
import eu.grandmedical.diten.mobile.core.database.DitenDatabase
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner

/**
 * Proves the schema-export + migration-test harness actually works: the helper
 * opens the exported v1 schema JSON (from `schemas/`, wired in as test assets)
 * and materializes a real database at version 1.
 *
 * When the first real migration lands (v2), this class gains a
 * `migrate1To2()` test that calls
 * `helper.runMigrationsAndValidate(TEST_DB, 2, true, DitenMigrations.MIGRATION_1_2)`.
 */
@RunWith(RobolectricTestRunner::class)
class DitenDatabaseMigrationTest {

    @get:Rule
    val helper = MigrationTestHelper(
        InstrumentationRegistry.getInstrumentation(),
        DitenDatabase::class.java,
    )

    @Test
    fun exportedV1Schema_opens() {
        // Throws if the exported schema JSON is missing or unreadable, which would
        // mean schema export or the asset wiring regressed.
        val db = helper.createDatabase(TEST_DB, DitenDatabase.DB_VERSION)

        assertTrue(db.isOpen)
        // The partitioned cache table exists at v1.
        db.query("SELECT name FROM sqlite_master WHERE type='table' AND name='cached_readiness'")
            .use { cursor ->
                assertTrue("cached_readiness table missing from v1 schema", cursor.moveToFirst())
            }
        db.close()
    }

    private companion object {
        const val TEST_DB = "diten-migration-test.db"
    }
}
