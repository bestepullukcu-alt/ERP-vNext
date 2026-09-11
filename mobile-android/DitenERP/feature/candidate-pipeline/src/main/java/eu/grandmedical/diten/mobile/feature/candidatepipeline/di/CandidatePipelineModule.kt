package eu.grandmedical.diten.mobile.feature.candidatepipeline.di

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
import eu.grandmedical.diten.mobile.feature.candidatepipeline.data.CandidatePipelineRepositoryImpl
import eu.grandmedical.diten.mobile.feature.candidatepipeline.data.api.CandidatePipelineApi
import eu.grandmedical.diten.mobile.feature.candidatepipeline.data.local.CandidatePipelineDao
import eu.grandmedical.diten.mobile.feature.candidatepipeline.data.local.CandidatePipelineDatabase
import eu.grandmedical.diten.mobile.feature.candidatepipeline.data.scope.CandidatePipelineScopeProvider
import eu.grandmedical.diten.mobile.feature.candidatepipeline.data.scope.SessionScopeProvider
import eu.grandmedical.diten.mobile.feature.candidatepipeline.data.sync.CandidatePipelineSyncHandler
import eu.grandmedical.diten.mobile.feature.candidatepipeline.domain.CandidatePipelineRepository
import retrofit2.Retrofit
import javax.inject.Singleton

/**
 * Provides the feature's own Room database, DAO and Retrofit Api (built from the
 * `Retrofit` singleton supplied by `:core:network`'s `NetworkModule`).
 */
@Module
@InstallIn(SingletonComponent::class)
object CandidatePipelineProvidesModule {

    @Provides
    @Singleton
    fun provideDatabase(
        @ApplicationContext context: Context,
    ): CandidatePipelineDatabase =
        Room.databaseBuilder(
            context.applicationContext,
            CandidatePipelineDatabase::class.java,
            CandidatePipelineDatabase.DB_NAME,
        ).build()

    @Provides
    @Singleton
    fun provideDao(database: CandidatePipelineDatabase): CandidatePipelineDao =
        database.candidatePipelineDao()

    @Provides
    @Singleton
    fun provideApi(retrofit: Retrofit): CandidatePipelineApi =
        retrofit.create(CandidatePipelineApi::class.java)
}

/**
 * Binds the feature's implementations, and contributes its [SyncHandler] to the
 * app-wide `Set<SyncHandler>` (`@IntoSet`) that
 * [SyncEngine][eu.grandmedical.diten.mobile.core.sync.SyncEngine] runs.
 */
@Module
@InstallIn(SingletonComponent::class)
interface CandidatePipelineBindsModule {

    @Binds
    @Singleton
    fun bindRepository(impl: CandidatePipelineRepositoryImpl): CandidatePipelineRepository

    @Binds
    @Singleton
    fun bindScopeProvider(impl: SessionScopeProvider): CandidatePipelineScopeProvider

    @Binds
    @IntoSet
    fun bindSyncHandler(impl: CandidatePipelineSyncHandler): SyncHandler
}
