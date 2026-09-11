package eu.grandmedical.diten.mobile.feature.timeattendanceleave.data.local

import androidx.room.Database
import androidx.room.RoomDatabase
import androidx.room.TypeConverters
import eu.grandmedical.diten.mobile.core.database.converter.DitenTypeConverters

/**
 * This feature owns its OWN Room database (it does not touch `DitenDatabase`).
 *
 * Reuses the shared [DitenTypeConverters] (Instant / SyncStatus) and adds the
 * feature-local [TimeAttendanceLeaveConverters] for the dependency-states map.
 * `exportSchema = true` writes the schema to this module's `schemas/` dir.
 */
@Database(
    entities = [TimeAttendanceLeaveEntity::class],
    version = TimeAttendanceLeaveDatabase.DB_VERSION,
    exportSchema = true,
)
@TypeConverters(DitenTypeConverters::class, TimeAttendanceLeaveConverters::class)
abstract class TimeAttendanceLeaveDatabase : RoomDatabase() {

    abstract fun timeAttendanceLeaveDao(): TimeAttendanceLeaveDao

    companion object {
        const val DB_VERSION = 1
        const val DB_NAME = "time_attendance_leave.db"
    }
}
