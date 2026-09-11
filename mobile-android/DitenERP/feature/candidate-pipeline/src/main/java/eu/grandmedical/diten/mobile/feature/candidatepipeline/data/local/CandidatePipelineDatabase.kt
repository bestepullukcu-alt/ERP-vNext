package eu.grandmedical.diten.mobile.feature.candidatepipeline.data.local

import androidx.room.Database
import androidx.room.RoomDatabase
import androidx.room.TypeConverters
import eu.grandmedical.diten.mobile.core.database.converter.DitenTypeConverters

/**
 * This feature owns its OWN Room database (it does not touch `DitenDatabase`).
 *
 * Reuses the shared [DitenTypeConverters] (Instant / SyncStatus) and adds the
 * feature-local [CandidatePipelineConverters] for the dependency-states map.
 * `exportSchema = true` writes the schema to this module's `schemas/` dir.
 */
@Database(
    entities = [CandidatePipelineEntity::class],
    version = CandidatePipelineDatabase.DB_VERSION,
    exportSchema = true,
)
@TypeConverters(DitenTypeConverters::class, CandidatePipelineConverters::class)
abstract class CandidatePipelineDatabase : RoomDatabase() {

    abstract fun candidatePipelineDao(): CandidatePipelineDao

    companion object {
        const val DB_VERSION = 1
        const val DB_NAME = "candidate_pipeline.db"
    }
}
