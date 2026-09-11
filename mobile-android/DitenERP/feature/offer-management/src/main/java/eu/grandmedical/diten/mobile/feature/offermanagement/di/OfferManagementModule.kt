package eu.grandmedical.diten.mobile.feature.offermanagement.di

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
import eu.grandmedical.diten.mobile.feature.offermanagement.data.OfferManagementRepositoryImpl
import eu.grandmedical.diten.mobile.feature.offermanagement.data.api.OfferManagementApi
import eu.grandmedical.diten.mobile.feature.offermanagement.data.local.OfferManagementDao
import eu.grandmedical.diten.mobile.feature.offermanagement.data.local.OfferManagementDatabase
import eu.grandmedical.diten.mobile.feature.offermanagement.data.scope.OfferManagementScopeProvider
import eu.grandmedical.diten.mobile.feature.offermanagement.data.scope.SessionScopeProvider
import eu.grandmedical.diten.mobile.feature.offermanagement.data.sync.OfferManagementSyncHandler
import eu.grandmedical.diten.mobile.feature.offermanagement.domain.OfferManagementRepository
import retrofit2.Retrofit
import javax.inject.Singleton

/**
 * Provides the feature's own Room database, DAO and Retrofit Api (built from the
 * `Retrofit` singleton supplied by `:core:network`'s `NetworkModule`).
 */
@Module
@InstallIn(SingletonComponent::class)
object OfferManagementProvidesModule {

    @Provides
    @Singleton
    fun provideDatabase(
        @ApplicationContext context: Context,
    ): OfferManagementDatabase =
        Room.databaseBuilder(
            context.applicationContext,
            OfferManagementDatabase::class.java,
            OfferManagementDatabase.DB_NAME,
        ).build()

    @Provides
    @Singleton
    fun provideDao(database: OfferManagementDatabase): OfferManagementDao =
        database.offerManagementDao()

    @Provides
    @Singleton
    fun provideApi(retrofit: Retrofit): OfferManagementApi =
        retrofit.create(OfferManagementApi::class.java)
}

/**
 * Binds the feature's implementations, and contributes its [SyncHandler] to the
 * app-wide `Set<SyncHandler>` (`@IntoSet`) that
 * [SyncEngine][eu.grandmedical.diten.mobile.core.sync.SyncEngine] runs.
 */
@Module
@InstallIn(SingletonComponent::class)
interface OfferManagementBindsModule {

    @Binds
    @Singleton
    fun bindRepository(impl: OfferManagementRepositoryImpl): OfferManagementRepository

    @Binds
    @Singleton
    fun bindScopeProvider(impl: SessionScopeProvider): OfferManagementScopeProvider

    @Binds
    @IntoSet
    fun bindSyncHandler(impl: OfferManagementSyncHandler): SyncHandler
}
