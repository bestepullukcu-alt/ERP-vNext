package eu.grandmedical.diten.mobile.feature.applicantintake.di

import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import dagger.multibindings.IntoSet
import eu.grandmedical.diten.mobile.core.common.navigation.FeatureEntry
import eu.grandmedical.diten.mobile.core.feature.FeatureNavGraph
import eu.grandmedical.diten.mobile.feature.applicantintake.presentation.navigation.ApplicantIntakeFeature

/**
 * Contributes the Applicant Intake feature to the app shell through the
 * feature-plugin contract — with ZERO `:app` edits. Both bindings are `@IntoSet`,
 * so they flow into the shell's injected `Set<FeatureEntry>` (Home menu) and
 * `Set<FeatureNavGraph>` (NavHost destinations) automatically.
 */
@Module
@InstallIn(SingletonComponent::class)
object ApplicantIntakeFeatureModule {

    /** Menu half: the permission-gated Home entry. */
    @Provides
    @IntoSet
    fun provideFeatureEntry(): FeatureEntry = ApplicantIntakeFeature.entry

    /** Nav half: contributes the list / create / detail destinations. */
    @Provides
    @IntoSet
    fun provideFeatureNavGraph(): FeatureNavGraph =
        FeatureNavGraph { builder, navController ->
            ApplicantIntakeFeature.register(builder, navController)
        }
}
