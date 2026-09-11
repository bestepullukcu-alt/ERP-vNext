package eu.grandmedical.diten.mobile.feature.compensationbenefits.di

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
import eu.grandmedical.diten.mobile.feature.compensationbenefits.data.CompensationBenefitsRepositoryImpl
import eu.grandmedical.diten.mobile.feature.compensationbenefits.data.api.CompensationBenefitsApi
import eu.grandmedical.diten.mobile.feature.compensationbenefits.data.local.CompensationBenefitsDao
import eu.grandmedical.diten.mobile.feature.compensationbenefits.data.local.CompensationBenefitsDatabase
import eu.grandmedical.diten.mobile.feature.compensationbenefits.data.scope.CompensationBenefitsScopeProvider
import eu.grandmedical.diten.mobile.feature.compensationbenefits.data.scope.SessionScopeProvider
import eu.grandmedical.diten.mobile.feature.compensationbenefits.data.sync.CompensationBenefitsSyncHandler
import eu.grandmedical.diten.mobile.feature.compensationbenefits.domain.CompensationBenefitsRepository
import retrofit2.Retrofit
import javax.inject.Singleton

/**
 * Provides the feature's own Room database, DAO and Retrofit Api (built from the
 * `Retrofit` singleton supplied by `:core:network`'s `NetworkModule`).
 */
@Module
@InstallIn(SingletonComponent::class)
object CompensationBenefitsProvidesModule {

    @Provides
    @Singleton
    fun provideDatabase(
        @ApplicationContext context: Context,
    ): CompensationBenefitsDatabase =
        Room.databaseBuilder(
            context.applicationContext,
            CompensationBenefitsDatabase::class.java,
            CompensationBenefitsDatabase.DB_NAME,
        ).build()

    @Provides
    @Singleton
    fun provideDao(database: CompensationBenefitsDatabase): CompensationBenefitsDao =
        database.compensationBenefitsDao()

    @Provides
    @Singleton
    fun provideApi(retrofit: Retrofit): CompensationBenefitsApi =
        retrofit.create(CompensationBenefitsApi::class.java)
}

/**
 * Binds the feature's implementations, and contributes its [SyncHandler] to the
 * app-wide `Set<SyncHandler>` (`@IntoSet`) that
 * [SyncEngine][eu.grandmedical.diten.mobile.core.sync.SyncEngine] runs.
 */
@Module
@InstallIn(SingletonComponent::class)
interface CompensationBenefitsBindsModule {

    @Binds
    @Singleton
    fun bindRepository(impl: CompensationBenefitsRepositoryImpl): CompensationBenefitsRepository

    @Binds
    @Singleton
    fun bindScopeProvider(impl: SessionScopeProvider): CompensationBenefitsScopeProvider

    @Binds
    @IntoSet
    fun bindSyncHandler(impl: CompensationBenefitsSyncHandler): SyncHandler
}
