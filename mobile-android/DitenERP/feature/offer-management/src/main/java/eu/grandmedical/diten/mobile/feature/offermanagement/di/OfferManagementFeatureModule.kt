package eu.grandmedical.diten.mobile.feature.offermanagement.di

import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import dagger.multibindings.IntoSet
import eu.grandmedical.diten.mobile.core.common.navigation.FeatureEntry
import eu.grandmedical.diten.mobile.core.feature.FeatureNavGraph
import eu.grandmedical.diten.mobile.feature.offermanagement.presentation.navigation.OfferManagementFeature

/**
 * Contributes the Offer Management feature to the app shell through the
 * feature-plugin contract — with ZERO `:app` edits. Both bindings are `@IntoSet`,
 * so they flow into the shell's injected `Set<FeatureEntry>` (Home menu) and
 * `Set<FeatureNavGraph>` (NavHost destinations) automatically.
 */
@Module
@InstallIn(SingletonComponent::class)
object OfferManagementFeatureModule {

    /** Menu half: the permission-gated Home entry. */
    @Provides
    @IntoSet
    fun provideFeatureEntry(): FeatureEntry = OfferManagementFeature.entry

    /** Nav half: contributes the list / create / detail destinations. */
    @Provides
    @IntoSet
    fun provideFeatureNavGraph(): FeatureNavGraph =
        FeatureNavGraph { builder, navController ->
            OfferManagementFeature.register(builder, navController)
        }
}
