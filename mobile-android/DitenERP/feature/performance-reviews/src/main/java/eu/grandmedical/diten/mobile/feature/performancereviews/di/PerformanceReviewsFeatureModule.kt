package eu.grandmedical.diten.mobile.feature.performancereviews.di

import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import dagger.multibindings.IntoSet
import eu.grandmedical.diten.mobile.core.common.navigation.FeatureEntry
import eu.grandmedical.diten.mobile.core.feature.FeatureNavGraph
import eu.grandmedical.diten.mobile.feature.performancereviews.presentation.navigation.PerformanceReviewsFeature

/**
 * Contributes the Performance Reviews feature to the app shell through the
 * feature-plugin contract — with ZERO `:app` edits. Both bindings are `@IntoSet`,
 * so they flow into the shell's injected `Set<FeatureEntry>` (Home menu) and
 * `Set<FeatureNavGraph>` (NavHost destinations) automatically.
 */
@Module
@InstallIn(SingletonComponent::class)
object PerformanceReviewsFeatureModule {

    /** Menu half: the permission-gated Home entry. */
    @Provides
    @IntoSet
    fun provideFeatureEntry(): FeatureEntry = PerformanceReviewsFeature.entry

    /** Nav half: contributes the list / create / detail destinations. */
    @Provides
    @IntoSet
    fun provideFeatureNavGraph(): FeatureNavGraph =
        FeatureNavGraph { builder, navController ->
            PerformanceReviewsFeature.register(builder, navController)
        }
}
