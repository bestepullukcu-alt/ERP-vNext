package eu.grandmedical.diten.mobile.feature.employeeonboarding.di

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
import eu.grandmedical.diten.mobile.feature.employeeonboarding.data.EmployeeOnboardingRepositoryImpl
import eu.grandmedical.diten.mobile.feature.employeeonboarding.data.api.EmployeeOnboardingApi
import eu.grandmedical.diten.mobile.feature.employeeonboarding.data.local.EmployeeOnboardingDao
import eu.grandmedical.diten.mobile.feature.employeeonboarding.data.local.EmployeeOnboardingDatabase
import eu.grandmedical.diten.mobile.feature.employeeonboarding.data.scope.EmployeeOnboardingScopeProvider
import eu.grandmedical.diten.mobile.feature.employeeonboarding.data.scope.SessionScopeProvider
import eu.grandmedical.diten.mobile.feature.employeeonboarding.data.sync.EmployeeOnboardingSyncHandler
import eu.grandmedical.diten.mobile.feature.employeeonboarding.domain.EmployeeOnboardingRepository
import retrofit2.Retrofit
import javax.inject.Singleton

/**
 * Provides the feature's own Room database, DAO and Retrofit Api (built from the
 * `Retrofit` singleton supplied by `:core:network`'s `NetworkModule`).
 */
@Module
@InstallIn(SingletonComponent::class)
object EmployeeOnboardingProvidesModule {

    @Provides
    @Singleton
    fun provideDatabase(
        @ApplicationContext context: Context,
    ): EmployeeOnboardingDatabase =
        Room.databaseBuilder(
            context.applicationContext,
            EmployeeOnboardingDatabase::class.java,
            EmployeeOnboardingDatabase.DB_NAME,
        ).build()

    @Provides
    @Singleton
    fun provideDao(database: EmployeeOnboardingDatabase): EmployeeOnboardingDao =
        database.employeeOnboardingDao()

    @Provides
    @Singleton
    fun provideApi(retrofit: Retrofit): EmployeeOnboardingApi =
        retrofit.create(EmployeeOnboardingApi::class.java)
}

/**
 * Binds the feature's implementations, and contributes its [SyncHandler] to the
 * app-wide `Set<SyncHandler>` (`@IntoSet`) that
 * [SyncEngine][eu.grandmedical.diten.mobile.core.sync.SyncEngine] runs.
 */
@Module
@InstallIn(SingletonComponent::class)
interface EmployeeOnboardingBindsModule {

    @Binds
    @Singleton
    fun bindRepository(impl: EmployeeOnboardingRepositoryImpl): EmployeeOnboardingRepository

    @Binds
    @Singleton
    fun bindScopeProvider(impl: SessionScopeProvider): EmployeeOnboardingScopeProvider

    @Binds
    @IntoSet
    fun bindSyncHandler(impl: EmployeeOnboardingSyncHandler): SyncHandler
}
