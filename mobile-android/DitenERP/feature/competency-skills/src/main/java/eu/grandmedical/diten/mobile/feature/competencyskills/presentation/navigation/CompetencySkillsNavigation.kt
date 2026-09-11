@file:Suppress("MatchingDeclarationName") // File groups the feature's nav entry + graph by intent.

package eu.grandmedical.diten.mobile.feature.competencyskills.presentation.navigation

import androidx.navigation.NavController
import androidx.navigation.NavGraphBuilder
import androidx.navigation.NavType
import androidx.navigation.compose.composable
import androidx.navigation.navArgument
import eu.grandmedical.diten.mobile.core.common.navigation.FeatureEntry
import eu.grandmedical.diten.mobile.feature.competencyskills.presentation.create.CompetencySkillsCreateRoute
import eu.grandmedical.diten.mobile.feature.competencyskills.presentation.detail.CompetencySkillsDetailRoute
import eu.grandmedical.diten.mobile.feature.competencyskills.presentation.list.CompetencySkillsListRoute

/**
 * The Competency & Skills feature's plugin contribution.
 *
 * It supplies both halves of the feature-plugin contract, which are wired into
 * the app shell via `@IntoSet` Hilt bindings in
 * [eu.grandmedical.diten.mobile.feature.competencyskills.di.CompetencySkillsFeatureModule]:
 *  - [entry]: the Compose-free [FeatureEntry] menu descriptor (gated by
 *    `hcm.competency-skills`).
 *  - [register]: contributes the list -> create / detail Compose destinations to
 *    the app `NavHost` (the "nav" half, adapted to a `FeatureNavGraph`).
 *
 * `:app` no longer references this object directly — discovery is entirely
 * through the injected `Set<FeatureEntry>` / `Set<FeatureNavGraph>`.
 */
object CompetencySkillsFeature {

    /** Stable feature key. */
    const val KEY = "competency-skills"

    /** Root/list route — the destination the app menu navigates to. */
    const val ROUTE = "competency-skills"

    private const val CREATE_ROUTE = "competency-skills/create"
    const val ARG_ID = "id"
    private const val DETAIL_ROUTE = "competency-skills/detail/{$ARG_ID}"

    private fun detailRouteFor(id: String): String = "competency-skills/detail/$id"

    /** The module's single, permission-gated menu entry. */
    val entry: FeatureEntry = FeatureEntry(
        key = KEY,
        route = ROUTE,
        title = "Yetkinlik & Beceriler",
        requiredPermission = "hcm.competency-skills",
    )

    /** Registers the list -> create / detail sub-graph into the app's `NavHost`. */
    fun register(builder: NavGraphBuilder, navController: NavController) {
        builder.composable(ROUTE) {
            CompetencySkillsListRoute(
                onOpenDetail = { id -> navController.navigate(detailRouteFor(id)) },
                onOpenCreate = { navController.navigate(CREATE_ROUTE) },
                onBack = { navController.popBackStack() },
            )
        }

        builder.composable(CREATE_ROUTE) {
            CompetencySkillsCreateRoute(onDone = { navController.popBackStack() })
        }

        builder.composable(
            route = DETAIL_ROUTE,
            arguments = listOf(navArgument(ARG_ID) { type = NavType.StringType }),
        ) {
            CompetencySkillsDetailRoute(onBack = { navController.popBackStack() })
        }
    }
}
