package eu.grandmedical.diten.mobile.navigation

import eu.grandmedical.diten.mobile.core.common.navigation.NavRoute

/**
 * The app-shell navigation graph, expressed on top of the framework-agnostic
 * [NavRoute] contract from :core:common. Route keys are the stable strings the
 * Compose [androidx.navigation.NavHost] is built from.
 */
sealed interface DitenDestination : NavRoute {

    /** Credential entry. Start destination while [AuthState] is Unauthenticated. */
    data object Login : DitenDestination {
        override val route: String = "login"
    }

    /** Second-factor code entry, reached from Login when the backend requires MFA. */
    data object Mfa : DitenDestination {
        const val ARG_CHALLENGE_ID: String = "challengeId"
        override val route: String = "mfa/{$ARG_CHALLENGE_ID}"

        /** Concrete route for a specific [challengeId]. */
        fun routeFor(challengeId: String): String = "mfa/$challengeId"
    }

    /** Dashboard + module menu. Start destination while authenticated. */
    data object Home : DitenDestination {
        override val route: String = "home"
    }

    // Feature destinations are no longer declared here: each feature module owns
    // its own routes and contributes them via a FeatureNavGraph (see AppRoot).
    // The old `module/{moduleKey}` placeholder route was removed with the static
    // HomeModules catalogue.
}
