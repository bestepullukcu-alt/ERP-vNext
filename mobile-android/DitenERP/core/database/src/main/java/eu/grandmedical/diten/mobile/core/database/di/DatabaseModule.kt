package eu.grandmedical.diten.mobile.core.database.di

import android.content.Context
import androidx.room.Room
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.android.qualifiers.ApplicationContext
import dagger.hilt.components.SingletonComponent
import eu.grandmedical.diten.mobile.core.database.DitenDatabase
import eu.grandmedical.diten.mobile.core.database.dao.CachedReadinessDao
import eu.grandmedical.diten.mobile.core.database.migration.DitenMigrations
import javax.inject.Singleton

/**
 * Provides the singleton [DitenDatabase] and its DAOs to the app graph.
 *
 * Main-thread queries are intentionally left disabled (the Room default), so a
 * stray query on the UI thread fails fast in development instead of janking the
 * app in production. Tests build their own in-memory database with
 * `allowMainThreadQueries()`; production never does.
 */
@Module
@InstallIn(SingletonComponent::class)
object DatabaseModule {

    @Provides
    @Singleton
    fun provideDitenDatabase(
        @ApplicationContext context: Context,
    ): DitenDatabase {
        val builder = Room.databaseBuilder(
            context.applicationContext,
            DitenDatabase::class.java,
            DitenDatabase.DB_NAME,
        )
        // Explicit migrations only; no destructive fallback in production.
        DitenMigrations.ALL.forEach { builder.addMigrations(it) }
        return builder.build()
    }

    @Provides
    @Singleton
    fun provideCachedReadinessDao(database: DitenDatabase): CachedReadinessDao =
        database.cachedReadinessDao()
}
