package eu.grandmedical.diten.mobile.feature.performancereviews.di

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
import eu.grandmedical.diten.mobile.feature.performancereviews.data.PerformanceReviewsRepositoryImpl
import eu.grandmedical.diten.mobile.feature.performancereviews.data.api.PerformanceReviewsApi
import eu.grandmedical.diten.mobile.feature.performancereviews.data.local.PerformanceReviewsDao
import eu.grandmedical.diten.mobile.feature.performancereviews.data.local.PerformanceReviewsDatabase
import eu.grandmedical.diten.mobile.feature.performancereviews.data.scope.PerformanceReviewsScopeProvider
import eu.grandmedical.diten.mobile.feature.performancereviews.data.scope.SessionScopeProvider
import eu.grandmedical.diten.mobile.feature.performancereviews.data.sync.PerformanceReviewsSyncHandler
import eu.grandmedical.diten.mobile.feature.performancereviews.domain.PerformanceReviewsRepository
import retrofit2.Retrofit
import javax.inject.Singleton

/**
 * Provides the feature's own Room database, DAO and Retrofit Api (built from the
 * `Retrofit` singleton supplied by `:core:network`'s `NetworkModule`).
 */
@Module
@InstallIn(SingletonComponent::class)
object PerformanceReviewsProvidesModule {

    @Provides
    @Singleton
    fun provideDatabase(
        @ApplicationContext context: Context,
    ): PerformanceReviewsDatabase =
        Room.databaseBuilder(
            context.applicationContext,
            PerformanceReviewsDatabase::class.java,
            PerformanceReviewsDatabase.DB_NAME,
        ).build()

    @Provides
    @Singleton
    fun provideDao(database: PerformanceReviewsDatabase): PerformanceReviewsDao =
        database.performanceReviewsDao()

    @Provides
    @Singleton
    fun provideApi(retrofit: Retrofit): PerformanceReviewsApi =
        retrofit.create(PerformanceReviewsApi::class.java)
}

/**
 * Binds the feature's implementations, and contributes its [SyncHandler] to the
 * app-wide `Set<SyncHandler>` (`@IntoSet`) that
 * [SyncEngine][eu.grandmedical.diten.mobile.core.sync.SyncEngine] runs.
 */
@Module
@InstallIn(SingletonComponent::class)
interface PerformanceReviewsBindsModule {

    @Binds
    @Singleton
    fun bindRepository(impl: PerformanceReviewsRepositoryImpl): PerformanceReviewsRepository

    @Binds
    @Singleton
    fun bindScopeProvider(impl: SessionScopeProvider): PerformanceReviewsScopeProvider

    @Binds
    @IntoSet
    fun bindSyncHandler(impl: PerformanceReviewsSyncHandler): SyncHandler
}
