package eu.grandmedical.diten.mobile.feature.learningtraining.di

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
import eu.grandmedical.diten.mobile.feature.learningtraining.data.LearningTrainingRepositoryImpl
import eu.grandmedical.diten.mobile.feature.learningtraining.data.api.LearningTrainingApi
import eu.grandmedical.diten.mobile.feature.learningtraining.data.local.LearningTrainingDao
import eu.grandmedical.diten.mobile.feature.learningtraining.data.local.LearningTrainingDatabase
import eu.grandmedical.diten.mobile.feature.learningtraining.data.scope.LearningTrainingScopeProvider
import eu.grandmedical.diten.mobile.feature.learningtraining.data.scope.SessionScopeProvider
import eu.grandmedical.diten.mobile.feature.learningtraining.data.sync.LearningTrainingSyncHandler
import eu.grandmedical.diten.mobile.feature.learningtraining.domain.LearningTrainingRepository
import retrofit2.Retrofit
import javax.inject.Singleton

/**
 * Provides the feature's own Room database, DAO and Retrofit Api (built from the
 * `Retrofit` singleton supplied by `:core:network`'s `NetworkModule`).
 */
@Module
@InstallIn(SingletonComponent::class)
object LearningTrainingProvidesModule {

    @Provides
    @Singleton
    fun provideDatabase(
        @ApplicationContext context: Context,
    ): LearningTrainingDatabase =
        Room.databaseBuilder(
            context.applicationContext,
            LearningTrainingDatabase::class.java,
            LearningTrainingDatabase.DB_NAME,
        ).build()

    @Provides
    @Singleton
    fun provideDao(database: LearningTrainingDatabase): LearningTrainingDao =
        database.learningTrainingDao()

    @Provides
    @Singleton
    fun provideApi(retrofit: Retrofit): LearningTrainingApi =
        retrofit.create(LearningTrainingApi::class.java)
}

/**
 * Binds the feature's implementations, and contributes its [SyncHandler] to the
 * app-wide `Set<SyncHandler>` (`@IntoSet`) that
 * [SyncEngine][eu.grandmedical.diten.mobile.core.sync.SyncEngine] runs.
 */
@Module
@InstallIn(SingletonComponent::class)
interface LearningTrainingBindsModule {

    @Binds
    @Singleton
    fun bindRepository(impl: LearningTrainingRepositoryImpl): LearningTrainingRepository

    @Binds
    @Singleton
    fun bindScopeProvider(impl: SessionScopeProvider): LearningTrainingScopeProvider

    @Binds
    @IntoSet
    fun bindSyncHandler(impl: LearningTrainingSyncHandler): SyncHandler
}
