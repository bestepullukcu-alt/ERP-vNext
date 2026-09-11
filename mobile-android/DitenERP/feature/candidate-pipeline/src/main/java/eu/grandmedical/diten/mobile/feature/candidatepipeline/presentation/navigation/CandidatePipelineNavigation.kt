@file:Suppress("MatchingDeclarationName") // File groups the feature's nav entry + graph by intent.

package eu.grandmedical.diten.mobile.feature.candidatepipeline.presentation.navigation

import androidx.navigation.NavController
import androidx.navigation.NavGraphBuilder
import androidx.navigation.NavType
import androidx.navigation.compose.composable
import androidx.navigation.navArgument
import eu.grandmedical.diten.mobile.core.common.navigation.FeatureEntry
import eu.grandmedical.diten.mobile.feature.candidatepipeline.presentation.create.CandidatePipelineCreateRoute
import eu.grandmedical.diten.mobile.feature.candidatepipeline.presentation.detail.CandidatePipelineDetailRoute
import eu.grandmedical.diten.mobile.feature.candidatepipeline.presentation.list.CandidatePipelineListRoute

/**
 * The Candidate Pipeline feature's plugin contribution.
 *
 * It supplies both halves of the feature-plugin contract, which are wired into
 * the app shell via `@IntoSet` Hilt bindings in
 * [eu.grandmedical.diten.mobile.feature.candidatepipeline.di.CandidatePipelineFeatureModule]:
 *  - [entry]: the Compose-free [FeatureEntry] menu descriptor (gated by
 *    `hcm.candidate-pipeline`).
 *  - [register]: contributes the list -> create / detail Compose destinations to
 *    the app `NavHost` (the "nav" half, adapted to a `FeatureNavGraph`).
 *
 * `:app` no longer references this object directly — discovery is entirely
 * through the injected `Set<FeatureEntry>` / `Set<FeatureNavGraph>`.
 */
object CandidatePipelineFeature {

    /** Stable feature key. */
    const val KEY = "candidate-pipeline"

    /** Root/list route — the destination the app menu navigates to. */
    const val ROUTE = "candidate-pipeline"

    private const val CREATE_ROUTE = "candidate-pipeline/create"
    const val ARG_ID = "id"
    private const val DETAIL_ROUTE = "candidate-pipeline/detail/{$ARG_ID}"

    private fun detailRouteFor(id: String): String = "candidate-pipeline/detail/$id"

    /** The module's single, permission-gated menu entry. */
    val entry: FeatureEntry = FeatureEntry(
        key = KEY,
        route = ROUTE,
        title = "Aday Havuzu",
        requiredPermission = "hcm.candidate-pipeline",
    )

    /** Registers the list -> create / detail sub-graph into the app's `NavHost`. */
    fun register(builder: NavGraphBuilder, navController: NavController) {
        builder.composable(ROUTE) {
            CandidatePipelineListRoute(
                onOpenDetail = { id -> navController.navigate(detailRouteFor(id)) },
                onOpenCreate = { navController.navigate(CREATE_ROUTE) },
                onBack = { navController.popBackStack() },
            )
        }

        builder.composable(CREATE_ROUTE) {
            CandidatePipelineCreateRoute(onDone = { navController.popBackStack() })
        }

        builder.composable(
            route = DETAIL_ROUTE,
            arguments = listOf(navArgument(ARG_ID) { type = NavType.StringType }),
        ) {
            CandidatePipelineDetailRoute(onBack = { navController.popBackStack() })
        }
    }
}
