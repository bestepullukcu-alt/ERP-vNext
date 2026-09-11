@file:Suppress("MatchingDeclarationName") // File groups the feature's nav entry + graph by intent.

package eu.grandmedical.diten.mobile.feature.performancereviews.presentation.navigation

import androidx.navigation.NavController
import androidx.navigation.NavGraphBuilder
import androidx.navigation.NavType
import androidx.navigation.compose.composable
import androidx.navigation.navArgument
import eu.grandmedical.diten.mobile.core.common.navigation.FeatureEntry
import eu.grandmedical.diten.mobile.feature.performancereviews.presentation.create.PerformanceReviewsCreateRoute
import eu.grandmedical.diten.mobile.feature.performancereviews.presentation.detail.PerformanceReviewsDetailRoute
import eu.grandmedical.diten.mobile.feature.performancereviews.presentation.list.PerformanceReviewsListRoute

/**
 * The Performance Reviews feature's plugin contribution.
 *
 * It supplies both halves of the feature-plugin contract, which are wired into
 * the app shell via `@IntoSet` Hilt bindings in
 * [eu.grandmedical.diten.mobile.feature.performancereviews.di.PerformanceReviewsFeatureModule]:
 *  - [entry]: the Compose-free [FeatureEntry] menu descriptor (gated by
 *    `hcm.performance-reviews`).
 *  - [register]: contributes the list -> create / detail Compose destinations to
 *    the app `NavHost` (the "nav" half, adapted to a `FeatureNavGraph`).
 *
 * `:app` no longer references this object directly — discovery is entirely
 * through the injected `Set<FeatureEntry>` / `Set<FeatureNavGraph>`.
 */
object PerformanceReviewsFeature {

    /** Stable feature key. */
    const val KEY = "performance-reviews"

    /** Root/list route — the destination the app menu navigates to. */
    const val ROUTE = "performance-reviews"

    private const val CREATE_ROUTE = "performance-reviews/create"
    const val ARG_ID = "id"
    private const val DETAIL_ROUTE = "performance-reviews/detail/{$ARG_ID}"

    private fun detailRouteFor(id: String): String = "performance-reviews/detail/$id"

    /** The module's single, permission-gated menu entry. */
    val entry: FeatureEntry = FeatureEntry(
        key = KEY,
        route = ROUTE,
        title = "Performans Değerlendirme",
        requiredPermission = "hcm.performance-reviews",
    )

    /** Registers the list -> create / detail sub-graph into the app's `NavHost`. */
    fun register(builder: NavGraphBuilder, navController: NavController) {
        builder.composable(ROUTE) {
            PerformanceReviewsListRoute(
                onOpenDetail = { id -> navController.navigate(detailRouteFor(id)) },
                onOpenCreate = { navController.navigate(CREATE_ROUTE) },
                onBack = { navController.popBackStack() },
            )
        }

        builder.composable(CREATE_ROUTE) {
            PerformanceReviewsCreateRoute(onDone = { navController.popBackStack() })
        }

        builder.composable(
            route = DETAIL_ROUTE,
            arguments = listOf(navArgument(ARG_ID) { type = NavType.StringType }),
        ) {
            PerformanceReviewsDetailRoute(onBack = { navController.popBackStack() })
        }
    }
}
