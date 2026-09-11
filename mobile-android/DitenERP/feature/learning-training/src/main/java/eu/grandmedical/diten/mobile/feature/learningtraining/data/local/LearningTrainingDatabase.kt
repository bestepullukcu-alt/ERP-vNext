package eu.grandmedical.diten.mobile.feature.learningtraining.data.local

import androidx.room.Database
import androidx.room.RoomDatabase
import androidx.room.TypeConverters
import eu.grandmedical.diten.mobile.core.database.converter.DitenTypeConverters

/**
 * This feature owns its OWN Room database (it does not touch `DitenDatabase`).
 *
 * Reuses the shared [DitenTypeConverters] (Instant / SyncStatus) and adds the
 * feature-local [LearningTrainingConverters] for the dependency-states map.
 * `exportSchema = true` writes the schema to this module's `schemas/` dir.
 */
@Database(
    entities = [LearningTrainingEntity::class],
    version = LearningTrainingDatabase.DB_VERSION,
    exportSchema = true,
)
@TypeConverters(DitenTypeConverters::class, LearningTrainingConverters::class)
abstract class LearningTrainingDatabase : RoomDatabase() {

    abstract fun learningTrainingDao(): LearningTrainingDao

    companion object {
        const val DB_VERSION = 1
        const val DB_NAME = "learning_training.db"
    }
}
