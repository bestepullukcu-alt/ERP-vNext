@file:Suppress("MatchingDeclarationName") // File groups the feature's nav entry + graph by intent.

package eu.grandmedical.diten.mobile.feature.applicantintake.presentation.navigation

import androidx.navigation.NavController
import androidx.navigation.NavGraphBuilder
import androidx.navigation.NavType
import androidx.navigation.compose.composable
import androidx.navigation.navArgument
import eu.grandmedical.diten.mobile.core.common.navigation.FeatureEntry
import eu.grandmedical.diten.mobile.feature.applicantintake.presentation.create.ApplicantIntakeCreateRoute
import eu.grandmedical.diten.mobile.feature.applicantintake.presentation.detail.ApplicantIntakeDetailRoute
import eu.grandmedical.diten.mobile.feature.applicantintake.presentation.list.ApplicantIntakeListRoute

/**
 * The feature's contribution to the app navigation graph. `:app` calls
 * [registerApplicantIntakeGraph] inside its `NavHost` and navigates to
 * [ENTRY].route (the list) when the user opens the Applicant Intake menu item.
 *
 * The whole feature is gated by the `hcm.applicant-intake` permission (checked at
 * the shell via
 * [PermissionGate][eu.grandmedical.diten.mobile.core.common.permission.PermissionGate]).
 */
object ApplicantIntakeFeature {

    /** Root/list route — the destination the app menu navigates to. */
    const val ROUTE = "applicant-intake"

    private const val CREATE_ROUTE = "applicant-intake/create"
    const val ARG_ID = "id"
    private const val DETAIL_ROUTE = "applicant-intake/detail/{$ARG_ID}"

    private fun detailRouteFor(id: String): String = "applicant-intake/detail/$id"

    /** The module's single, permission-gated menu entry. */
    val ENTRY: FeatureEntry = FeatureEntry(
        route = ROUTE,
        title = "Aday Başvuru Alımı",
        requiredPermission = "hcm.applicant-intake",
    )

    /** Registers the list -> create / detail sub-graph into the app's [NavHost]. */
    fun NavGraphBuilder.registerApplicantIntakeGraph(navController: NavController) {
        composable(ROUTE) {
            ApplicantIntakeListRoute(
                onOpenDetail = { id -> navController.navigate(detailRouteFor(id)) },
                onOpenCreate = { navController.navigate(CREATE_ROUTE) },
                onBack = { navController.popBackStack() },
            )
        }

        composable(CREATE_ROUTE) {
            ApplicantIntakeCreateRoute(onDone = { navController.popBackStack() })
        }

        composable(
            route = DETAIL_ROUTE,
            arguments = listOf(navArgument(ARG_ID) { type = NavType.StringType }),
        ) {
            ApplicantIntakeDetailRoute(onBack = { navController.popBackStack() })
        }
    }
}
