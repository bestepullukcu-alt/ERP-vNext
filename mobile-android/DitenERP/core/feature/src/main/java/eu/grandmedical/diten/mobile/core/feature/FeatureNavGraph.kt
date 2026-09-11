package eu.grandmedical.diten.mobile.core.feature

import androidx.navigation.NavController
import androidx.navigation.NavGraphBuilder

/**
 * The Compose-aware "nav" half of the feature-plugin contract.
 *
 * A feature module implements this to contribute its own Compose destinations
 * (its list / create / detail sub-routes) into the single app-shell `NavHost`,
 * without `:app` knowing anything about the feature. The shell injects
 * `Set<@JvmSuppressWildcards FeatureNavGraph>` (a Hilt multibinding) and calls
 * [register] on every contribution while building its `NavHost`.
 *
 * The other half of the contract is
 * [eu.grandmedical.diten.mobile.core.common.navigation.FeatureEntry] — the
 * Compose-free menu descriptor. **Each feature contributes BOTH**, via Hilt:
 *
 * ```
 * @Module
 * @InstallIn(SingletonComponent::class)
 * object MyFeatureModule {
 *     @Provides @IntoSet fun entry(): FeatureEntry = MyFeature.entry     // menu
 *     @Provides @IntoSet fun navGraph(): FeatureNavGraph =               // nav
 *         FeatureNavGraph { builder, navController ->
 *             MyFeature.register(builder, navController)
 *         }
 * }
 * ```
 *
 * Adding a feature is therefore ZERO `:app` edits: the new module's `@IntoSet`
 * bindings flow into the shell's injected sets automatically.
 */
fun interface FeatureNavGraph {

    /**
     * Adds this feature's destinations to [builder] (the app `NavHost`'s
     * [NavGraphBuilder]). Use [navController] for intra-feature navigation
     * (list -> detail, etc.). Routes must be globally unique; prefer prefixing
     * them with the feature key.
     */
    fun register(builder: NavGraphBuilder, navController: NavController)
}
