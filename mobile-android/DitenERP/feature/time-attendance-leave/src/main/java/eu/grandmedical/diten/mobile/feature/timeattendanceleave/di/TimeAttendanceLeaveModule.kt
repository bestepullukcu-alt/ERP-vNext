package eu.grandmedical.diten.mobile.feature.timeattendanceleave.di

import android.content.Context
import androidx.room.Room
import dagger.Binds
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.android.qualifiers.ApplicationContext
import dagger.hilt.components.SingletonComponent
import dagger.multibindings.IntoSet
import eu.grandmedical.diten.mobile.core.sync.SyncHandler
import eu.grandmedical.diten.mobile.feature.timeattendanceleave.data.TimeAttendanceLeaveRepositoryImpl
import eu.grandmedical.diten.mobile.feature.timeattendanceleave.data.api.TimeAttendanceLeaveApi
import eu.grandmedical.diten.mobile.feature.timeattendanceleave.data.local.TimeAttendanceLeaveDao
import eu.grandmedical.diten.mobile.feature.timeattendanceleave.data.local.TimeAttendanceLeaveDatabase
import eu.grandmedical.diten.mobile.feature.timeattendanceleave.data.scope.SessionScopeProvider
import eu.grandmedical.diten.mobile.feature.timeattendanceleave.data.scope.TimeAttendanceLeaveScopeProvider
import eu.grandmedical.diten.mobile.feature.timeattendanceleave.data.sync.TimeAttendanceLeaveSyncHandler
import eu.grandmedical.diten.mobile.feature.timeattendanceleave.domain.TimeAttendanceLeaveRepository
import retrofit2.Retrofit
import javax.inject.Singleton

/**
 * Provides the feature's own Room database, DAO and Retrofit Api (built from the
 * `Retrofit` singleton supplied by `:core:network`'s `NetworkModule`).
 */
@Module
@InstallIn(SingletonComponent::class)
object TimeAttendanceLeaveProvidesModule {

    @Provides
    @Singleton
    fun provideDatabase(
        @ApplicationContext context: Context,
    ): TimeAttendanceLeaveDatabase =
        Room.databaseBuilder(
            context.applicationContext,
            TimeAttendanceLeaveDatabase::class.java,
            TimeAttendanceLeaveDatabase.DB_NAME,
        ).build()

    @Provides
    @Singleton
    fun provideDao(database: TimeAttendanceLeaveDatabase): TimeAttendanceLeaveDao =
        database.timeAttendanceLeaveDao()

    @Provides
    @Singleton
    fun provideApi(retrofit: Retrofit): TimeAttendanceLeaveApi =
        retrofit.create(TimeAttendanceLeaveApi::class.java)
}

/**
 * Binds the feature's implementations, and contributes its [SyncHandler] to the
 * app-wide `Set<SyncHandler>` (`@IntoSet`) that
 * [SyncEngine][eu.grandmedical.diten.mobile.core.sync.SyncEngine] runs.
 */
@Module
@InstallIn(SingletonComponent::class)
interface TimeAttendanceLeaveBindsModule {

    @Binds
    @Singleton
    fun bindRepository(impl: TimeAttendanceLeaveRepositoryImpl): TimeAttendanceLeaveRepository

    @Binds
    @Singleton
    fun bindScopeProvider(impl: SessionScopeProvider): TimeAttendanceLeaveScopeProvider

    @Binds
    @IntoSet
    fun bindSyncHandler(impl: TimeAttendanceLeaveSyncHandler): SyncHandler
}
