@file:Suppress("MatchingDeclarationName") // File groups the feature's nav entry + graph by intent.

package eu.grandmedical.diten.mobile.feature.employeeonboarding.presentation.navigation

import androidx.navigation.NavController
import androidx.navigation.NavGraphBuilder
import androidx.navigation.NavType
import androidx.navigation.compose.composable
import androidx.navigation.navArgument
import eu.grandmedical.diten.mobile.core.common.navigation.FeatureEntry
import eu.grandmedical.diten.mobile.feature.employeeonboarding.presentation.create.EmployeeOnboardingCreateRoute
import eu.grandmedical.diten.mobile.feature.employeeonboarding.presentation.detail.EmployeeOnboardingDetailRoute
import eu.grandmedical.diten.mobile.feature.employeeonboarding.presentation.list.EmployeeOnboardingListRoute

/**
 * The Employee Onboarding feature's plugin contribution.
 *
 * It supplies both halves of the feature-plugin contract, which are wired into
 * the app shell via `@IntoSet` Hilt bindings in
 * [eu.grandmedical.diten.mobile.feature.employeeonboarding.di.EmployeeOnboardingFeatureModule]:
 *  - [entry]: the Compose-free [FeatureEntry] menu descriptor (gated by
 *    `hcm.employee-onboarding`).
 *  - [register]: contributes the list -> create / detail Compose destinations to
 *    the app `NavHost` (the "nav" half, adapted to a `FeatureNavGraph`).
 *
 * `:app` no longer references this object directly — discovery is entirely
 * through the injected `Set<FeatureEntry>` / `Set<FeatureNavGraph>`.
 */
object EmployeeOnboardingFeature {

    /** Stable feature key. */
    const val KEY = "employee-onboarding"

    /** Root/list route — the destination the app menu navigates to. */
    const val ROUTE = "employee-onboarding"

    private const val CREATE_ROUTE = "employee-onboarding/create"
    const val ARG_ID = "id"
    private const val DETAIL_ROUTE = "employee-onboarding/detail/{$ARG_ID}"

    private fun detailRouteFor(id: String): String = "employee-onboarding/detail/$id"

    /** The module's single, permission-gated menu entry. */
    val entry: FeatureEntry = FeatureEntry(
        key = KEY,
        route = ROUTE,
        title = "Çalışan Oryantasyonu",
        requiredPermission = "hcm.employee-onboarding",
    )

    /** Registers the list -> create / detail sub-graph into the app's `NavHost`. */
    fun register(builder: NavGraphBuilder, navController: NavController) {
        builder.composable(ROUTE) {
            EmployeeOnboardingListRoute(
                onOpenDetail = { id -> navController.navigate(detailRouteFor(id)) },
                onOpenCreate = { navController.navigate(CREATE_ROUTE) },
                onBack = { navController.popBackStack() },
            )
        }

        builder.composable(CREATE_ROUTE) {
            EmployeeOnboardingCreateRoute(onDone = { navController.popBackStack() })
        }

        builder.composable(
            route = DETAIL_ROUTE,
            arguments = listOf(navArgument(ARG_ID) { type = NavType.StringType }),
        ) {
            EmployeeOnboardingDetailRoute(onBack = { navController.popBackStack() })
        }
    }
}
