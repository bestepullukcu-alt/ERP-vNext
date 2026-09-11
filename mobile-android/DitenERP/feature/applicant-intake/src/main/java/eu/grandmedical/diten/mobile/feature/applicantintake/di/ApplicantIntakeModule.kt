package eu.grandmedical.diten.mobile.feature.applicantintake.di

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
import eu.grandmedical.diten.mobile.feature.applicantintake.data.ApplicantIntakeRepositoryImpl
import eu.grandmedical.diten.mobile.feature.applicantintake.data.api.ApplicantIntakeApi
import eu.grandmedical.diten.mobile.feature.applicantintake.data.local.ApplicantIntakeDao
import eu.grandmedical.diten.mobile.feature.applicantintake.data.local.ApplicantIntakeDatabase
import eu.grandmedical.diten.mobile.feature.applicantintake.data.scope.ApplicantIntakeScopeProvider
import eu.grandmedical.diten.mobile.feature.applicantintake.data.scope.SessionScopeProvider
import eu.grandmedical.diten.mobile.feature.applicantintake.data.sync.ApplicantIntakeSyncHandler
import eu.grandmedical.diten.mobile.feature.applicantintake.domain.ApplicantIntakeRepository
import retrofit2.Retrofit
import javax.inject.Singleton

/**
 * Provides the feature's own Room database, DAO and Retrofit Api (built from the
 * `Retrofit` singleton supplied by `:core:network`'s `NetworkModule`).
 */
@Module
@InstallIn(SingletonComponent::class)
object ApplicantIntakeProvidesModule {

    @Provides
    @Singleton
    fun provideDatabase(
        @ApplicationContext context: Context,
    ): ApplicantIntakeDatabase =
        Room.databaseBuilder(
            context.applicationContext,
            ApplicantIntakeDatabase::class.java,
            ApplicantIntakeDatabase.DB_NAME,
        ).build()

    @Provides
    @Singleton
    fun provideDao(database: ApplicantIntakeDatabase): ApplicantIntakeDao =
        database.applicantIntakeDao()

    @Provides
    @Singleton
    fun provideApi(retrofit: Retrofit): ApplicantIntakeApi =
        retrofit.create(ApplicantIntakeApi::class.java)
}

/**
 * Binds the feature's implementations, and contributes its [SyncHandler] to the
 * app-wide `Set<SyncHandler>` (`@IntoSet`) that
 * [SyncEngine][eu.grandmedical.diten.mobile.core.sync.SyncEngine] runs.
 */
@Module
@InstallIn(SingletonComponent::class)
interface ApplicantIntakeBindsModule {

    @Binds
    @Singleton
    fun bindRepository(impl: ApplicantIntakeRepositoryImpl): ApplicantIntakeRepository

    @Binds
    @Singleton
    fun bindScopeProvider(impl: SessionScopeProvider): ApplicantIntakeScopeProvider

    @Binds
    @IntoSet
    fun bindSyncHandler(impl: ApplicantIntakeSyncHandler): SyncHandler
}
