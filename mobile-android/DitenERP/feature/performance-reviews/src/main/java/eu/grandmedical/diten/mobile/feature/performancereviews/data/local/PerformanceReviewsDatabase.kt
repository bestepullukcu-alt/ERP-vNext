package eu.grandmedical.diten.mobile.feature.performancereviews.data.local

import androidx.room.Database
import androidx.room.RoomDatabase
import androidx.room.TypeConverters
import eu.grandmedical.diten.mobile.core.database.converter.DitenTypeConverters

/**
 * This feature owns its OWN Room database (it does not touch `DitenDatabase`).
 *
 * Reuses the shared [DitenTypeConverters] (Instant / SyncStatus) and adds the
 * feature-local [PerformanceReviewsConverters] for the dependency-states map.
 * `exportSchema = true` writes the schema to this module's `schemas/` dir.
 */
@Database(
    entities = [PerformanceReviewsEntity::class],
    version = PerformanceReviewsDatabase.DB_VERSION,
    exportSchema = true,
)
@TypeConverters(DitenTypeConverters::class, PerformanceReviewsConverters::class)
abstract class PerformanceReviewsDatabase : RoomDatabase() {

    abstract fun performanceReviewsDao(): PerformanceReviewsDao

    companion object {
        const val DB_VERSION = 1
        const val DB_NAME = "performance_reviews.db"
    }
}
