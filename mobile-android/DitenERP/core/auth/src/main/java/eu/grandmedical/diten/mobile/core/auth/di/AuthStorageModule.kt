package eu.grandmedical.diten.mobile.core.auth.di

import android.content.Context
import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.PreferenceDataStoreFactory
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.preferencesDataStoreFile
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.android.qualifiers.ApplicationContext
import dagger.hilt.components.SingletonComponent
import javax.inject.Singleton

/**
 * Provides the single [DataStore] instance that backs the encrypted token store.
 * DataStore forbids more than one active instance per file, so this MUST be a
 * singleton keyed to one file name.
 */
@Module
@InstallIn(SingletonComponent::class)
object AuthStorageModule {

    private const val TOKEN_STORE_FILE = "diten_auth_tokens"

    @Provides
    @Singleton
    fun provideTokenDataStore(@ApplicationContext context: Context): DataStore<Preferences> =
        PreferenceDataStoreFactory.create(
            produceFile = { context.preferencesDataStoreFile(TOKEN_STORE_FILE) },
        )
}
