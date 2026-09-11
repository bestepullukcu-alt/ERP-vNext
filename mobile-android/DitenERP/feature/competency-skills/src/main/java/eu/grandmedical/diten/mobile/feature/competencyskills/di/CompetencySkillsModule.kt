package eu.grandmedical.diten.mobile.feature.competencyskills.di

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
import eu.grandmedical.diten.mobile.feature.competencyskills.data.CompetencySkillsRepositoryImpl
import eu.grandmedical.diten.mobile.feature.competencyskills.data.api.CompetencySkillsApi
import eu.grandmedical.diten.mobile.feature.competencyskills.data.local.CompetencySkillsDao
import eu.grandmedical.diten.mobile.feature.competencyskills.data.local.CompetencySkillsDatabase
import eu.grandmedical.diten.mobile.feature.competencyskills.data.scope.CompetencySkillsScopeProvider
import eu.grandmedical.diten.mobile.feature.competencyskills.data.scope.SessionScopeProvider
import eu.grandmedical.diten.mobile.feature.competencyskills.data.sync.CompetencySkillsSyncHandler
import eu.grandmedical.diten.mobile.feature.competencyskills.domain.CompetencySkillsRepository
import retrofit2.Retrofit
import javax.inject.Singleton

/**
 * Provides the feature's own Room database, DAO and Retrofit Api (built from the
 * `Retrofit` singleton supplied by `:core:network`'s `NetworkModule`).
 */
@Module
@InstallIn(SingletonComponent::class)
object CompetencySkillsProvidesModule {

    @Provides
    @Singleton
    fun provideDatabase(
        @ApplicationContext context: Context,
    ): CompetencySkillsDatabase =
        Room.databaseBuilder(
            context.applicationContext,
            CompetencySkillsDatabase::class.java,
            CompetencySkillsDatabase.DB_NAME,
        ).build()

    @Provides
    @Singleton
    fun provideDao(database: CompetencySkillsDatabase): CompetencySkillsDao =
        database.competencySkillsDao()

    @Provides
    @Singleton
    fun provideApi(retrofit: Retrofit): CompetencySkillsApi =
        retrofit.create(CompetencySkillsApi::class.java)
}

/**
 * Binds the feature's implementations, and contributes its [SyncHandler] to the
 * app-wide `Set<SyncHandler>` (`@IntoSet`) that
 * [SyncEngine][eu.grandmedical.diten.mobile.core.sync.SyncEngine] runs.
 */
@Module
@InstallIn(SingletonComponent::class)
interface CompetencySkillsBindsModule {

    @Binds
    @Singleton
    fun bindRepository(impl: CompetencySkillsRepositoryImpl): CompetencySkillsRepository

    @Binds
    @Singleton
    fun bindScopeProvider(impl: SessionScopeProvider): CompetencySkillsScopeProvider

    @Binds
    @IntoSet
    fun bindSyncHandler(impl: CompetencySkillsSyncHandler): SyncHandler
}
