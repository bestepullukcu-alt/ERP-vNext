package eu.grandmedical.diten.mobile.navigation

import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.navigation.NavController
import androidx.navigation.NavGraphBuilder
import androidx.navigation.NavType
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.rememberNavController
import androidx.navigation.navArgument
import eu.grandmedical.diten.mobile.core.auth.session.AuthState
import eu.grandmedical.diten.mobile.core.feature.FeatureNavGraph
import eu.grandmedical.diten.mobile.feature.home.HomeRoute
import eu.grandmedical.diten.mobile.feature.login.LoginRoute
import eu.grandmedical.diten.mobile.feature.login.MfaRoute

/**
 * The single-Activity navigation host.
 *
 * Start destination is chosen ONCE from the initial [AuthState]: [Home][DitenDestination.Home]
 * when authenticated (a restored session), otherwise [Login][DitenDestination.Login].
 * A session drop (logout / expiry) is observed from [AppViewModel.authState] and
 * routes back to Login with the whole back stack cleared. Forward navigation into
 * Home is driven by the Login/MFA one-shot effects, so it only happens on an
 * explicit successful login.
 *
 * Feature destinations are NOT wired here: after the fixed shell routes
 * (login/mfa/home) the `NavHost` iterates the injected
 * `Set<`[FeatureNavGraph]`>` (a Hilt multibinding) and lets each installed
 * feature register its own destinations. Adding a feature therefore needs zero
 * edits to this file.
 */
@Composable
fun AppRoot(
    appViewModel: AppViewModel = hiltViewModel(),
) {
    val navController = rememberNavController()
    val authState by appViewModel.authState.collectAsState()
    val featureNavGraphs = appViewModel.featureNavGraphs

    val startDestination = remember {
        if (appViewModel.authState.value is AuthState.Authenticated) {
            DitenDestination.Home.route
        } else {
            DitenDestination.Login.route
        }
    }

    LaunchedEffect(authState) {
        navController.reactToSession(authState)
    }

    NavHost(navController = navController, startDestination = startDestination) {
        ditenGraph(navController)
        // Auto-discovered feature destinations (each contributes via @IntoSet).
        featureNavGraphs.forEach { graph -> graph.register(this, navController) }
    }
}

/**
 * Reacts to session transitions, clearing the back stack each time:
 *  - Unauthenticated (logout / expiry) from any screen -> Login.
 *  - Authenticated WHILE on Login -> Home. This covers an async session restore
 *    on cold start (the initial state is Unauthenticated until the token store is
 *    read). The Login/MFA NavigateHome effects still drive interactive login;
 *    both target Home clearing Login, so they are idempotent (popUpTo +
 *    launchSingleTop).
 */
private fun NavController.reactToSession(authState: AuthState) {
    val currentRoute = currentDestination?.route
    when (authState) {
        is AuthState.Unauthenticated ->
            if (currentRoute != DitenDestination.Login.route) {
                navigate(DitenDestination.Login.route) {
                    popUpTo(0) { inclusive = true }
                    launchSingleTop = true
                }
            }

        is AuthState.Authenticated ->
            if (currentRoute == DitenDestination.Login.route) {
                navigate(DitenDestination.Home.route) {
                    popUpTo(0) { inclusive = true }
                    launchSingleTop = true
                }
            }
    }
}

/**
 * Declares the shell's fixed destinations (login / mfa / home). Feature
 * destinations are added separately by iterating the injected
 * [FeatureNavGraph] set — see [AppRoot].
 */
private fun NavGraphBuilder.ditenGraph(navController: NavController) {
    val toHomeClearingLogin: () -> Unit = {
        navController.navigate(DitenDestination.Home.route) {
            popUpTo(DitenDestination.Login.route) { inclusive = true }
            launchSingleTop = true
        }
    }

    composable(DitenDestination.Login.route) {
        LoginRoute(
            onNavigateHome = toHomeClearingLogin,
            onNavigateMfa = { challengeId ->
                navController.navigate(DitenDestination.Mfa.routeFor(challengeId))
            },
        )
    }

    composable(
        route = DitenDestination.Mfa.route,
        arguments = listOf(
            navArgument(DitenDestination.Mfa.ARG_CHALLENGE_ID) { type = NavType.StringType },
        ),
    ) {
        MfaRoute(onNavigateHome = toHomeClearingLogin)
    }

    composable(DitenDestination.Home.route) {
        HomeRoute(
            // The menu carries each feature's start-destination route directly, so
            // the shell just navigates to it — no per-feature branching.
            onOpenModule = { route -> navController.navigate(route) },
        )
    }
}
