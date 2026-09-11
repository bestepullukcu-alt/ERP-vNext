package eu.grandmedical.diten.mobile.feature.competencyskills.data.local

import androidx.room.Database
import androidx.room.RoomDatabase
import androidx.room.TypeConverters
import eu.grandmedical.diten.mobile.core.database.converter.DitenTypeConverters

/**
 * This feature owns its OWN Room database (it does not touch `DitenDatabase`).
 *
 * Reuses the shared [DitenTypeConverters] (Instant / SyncStatus) and adds the
 * feature-local [CompetencySkillsConverters] for the dependency-states map.
 * `exportSchema = true` writes the schema to this module's `schemas/` dir.
 */
@Database(
    entities = [CompetencySkillsEntity::class],
    version = CompetencySkillsDatabase.DB_VERSION,
    exportSchema = true,
)
@TypeConverters(DitenTypeConverters::class, CompetencySkillsConverters::class)
abstract class CompetencySkillsDatabase : RoomDatabase() {

    abstract fun competencySkillsDao(): CompetencySkillsDao

    companion object {
        const val DB_VERSION = 1
        const val DB_NAME = "competency_skills.db"
    }
}
