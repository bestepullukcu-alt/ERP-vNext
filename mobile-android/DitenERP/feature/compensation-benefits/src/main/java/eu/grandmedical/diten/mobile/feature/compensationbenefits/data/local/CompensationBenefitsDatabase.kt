package eu.grandmedical.diten.mobile.feature.compensationbenefits.data.local

import androidx.room.Database
import androidx.room.RoomDatabase
import androidx.room.TypeConverters
import eu.grandmedical.diten.mobile.core.database.converter.DitenTypeConverters

/**
 * This feature owns its OWN Room database (it does not touch `DitenDatabase`).
 *
 * Reuses the shared [DitenTypeConverters] (Instant / SyncStatus) and adds the
 * feature-local [CompensationBenefitsConverters] for the dependency-states map.
 * `exportSchema = true` writes the schema to this module's `schemas/` dir.
 */
@Database(
    entities = [CompensationBenefitsEntity::class],
    version = CompensationBenefitsDatabase.DB_VERSION,
    exportSchema = true,
)
@TypeConverters(DitenTypeConverters::class, CompensationBenefitsConverters::class)
abstract class CompensationBenefitsDatabase : RoomDatabase() {

    abstract fun compensationBenefitsDao(): CompensationBenefitsDao

    companion object {
        const val DB_VERSION = 1
        const val DB_NAME = "compensation_benefits.db"
    }
}
