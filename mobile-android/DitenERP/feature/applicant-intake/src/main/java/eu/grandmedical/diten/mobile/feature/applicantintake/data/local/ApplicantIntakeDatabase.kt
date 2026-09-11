package eu.grandmedical.diten.mobile.feature.applicantintake.data.local

import androidx.room.Database
import androidx.room.RoomDatabase
import androidx.room.TypeConverters
import eu.grandmedical.diten.mobile.core.database.converter.DitenTypeConverters

/**
 * This feature owns its OWN Room database (it does not touch `DitenDatabase`).
 *
 * Reuses the shared [DitenTypeConverters] (Instant / SyncStatus) and adds the
 * feature-local [ApplicantIntakeConverters] for the dependency-states map.
 * `exportSchema = true` writes the schema to this module's `schemas/` dir.
 */
@Database(
    entities = [ApplicantIntakeEntity::class],
    version = ApplicantIntakeDatabase.DB_VERSION,
    exportSchema = true,
)
@TypeConverters(DitenTypeConverters::class, ApplicantIntakeConverters::class)
abstract class ApplicantIntakeDatabase : RoomDatabase() {

    abstract fun applicantIntakeDao(): ApplicantIntakeDao

    companion object {
        const val DB_VERSION = 1
        const val DB_NAME = "applicant_intake.db"
    }
}
