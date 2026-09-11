@file:Suppress("MatchingDeclarationName") // File groups the feature's nav entry + graph by intent.

package eu.grandmedical.diten.mobile.feature.timeattendanceleave.presentation.navigation

import androidx.navigation.NavController
import androidx.navigation.NavGraphBuilder
import androidx.navigation.NavType
import androidx.navigation.compose.composable
import androidx.navigation.navArgument
import eu.grandmedical.diten.mobile.core.common.navigation.FeatureEntry
import eu.grandmedical.diten.mobile.feature.timeattendanceleave.presentation.create.TimeAttendanceLeaveCreateRoute
import eu.grandmedical.diten.mobile.feature.timeattendanceleave.presentation.detail.TimeAttendanceLeaveDetailRoute
import eu.grandmedical.diten.mobile.feature.timeattendanceleave.presentation.list.TimeAttendanceLeaveListRoute

/**
 * The Time & Leave Management feature's plugin contribution.
 *
 * It supplies both halves of the feature-plugin contract, which are wired into
 * the app shell via `@IntoSet` Hilt bindings in
 * [eu.grandmedical.diten.mobile.feature.timeattendanceleave.di.TimeAttendanceLeaveFeatureModule]:
 *  - [entry]: the Compose-free [FeatureEntry] menu descriptor (gated by
 *    `hcm.time-attendance-leave`).
 *  - [register]: contributes the list -> create / detail Compose destinations to
 *    the app `NavHost` (the "nav" half, adapted to a `FeatureNavGraph`).
 *
 * `:app` no longer references this object directly — discovery is entirely
 * through the injected `Set<FeatureEntry>` / `Set<FeatureNavGraph>`.
 */
object TimeAttendanceLeaveFeature {

    /** Stable feature key. */
    const val KEY = "time-attendance-leave"

    /** Root/list route — the destination the app menu navigates to. */
    const val ROUTE = "time-attendance-leave"

    private const val CREATE_ROUTE = "time-attendance-leave/create"
    const val ARG_ID = "id"
    private const val DETAIL_ROUTE = "time-attendance-leave/detail/{$ARG_ID}"

    private fun detailRouteFor(id: String): String = "time-attendance-leave/detail/$id"

    /** The module's single, permission-gated menu entry. */
    val entry: FeatureEntry = FeatureEntry(
        key = KEY,
        route = ROUTE,
        title = "Zaman & İzin Yönetimi",
        requiredPermission = "hcm.time-attendance-leave",
    )

    /** Registers the list -> create / detail sub-graph into the app's `NavHost`. */
    fun register(builder: NavGraphBuilder, navController: NavController) {
        builder.composable(ROUTE) {
            TimeAttendanceLeaveListRoute(
                onOpenDetail = { id -> navController.navigate(detailRouteFor(id)) },
                onOpenCreate = { navController.navigate(CREATE_ROUTE) },
                onBack = { navController.popBackStack() },
            )
        }

        builder.composable(CREATE_ROUTE) {
            TimeAttendanceLeaveCreateRoute(onDone = { navController.popBackStack() })
        }

        builder.composable(
            route = DETAIL_ROUTE,
            arguments = listOf(navArgument(ARG_ID) { type = NavType.StringType }),
        ) {
            TimeAttendanceLeaveDetailRoute(onBack = { navController.popBackStack() })
        }
    }
}
