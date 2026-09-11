package eu.grandmedical.diten.mobile.core.common.navigation

/**
 * The minimal, UI-framework-agnostic navigation contract shared by every module.
 *
 * Deliberately free of any Compose/Navigation dependency: a route is just a
 * stable string key. Feature modules build their real (typed/Compose) destinations
 * on top of this at the UI layer.
 */
interface NavRoute {
    /** Stable, unique route key for this destination. */
    val route: String
}

/**
 * A single destination a feature module contributes to the app's navigation graph.
 *
 * @property route the stable route key.
 * @property title human-readable label for menus/app bars.
 * @property requiredPermission the permission a user must hold for this entry to
 *   be reachable/visible (evaluated by
 *   [eu.grandmedical.diten.mobile.core.common.permission.PermissionGate]); `null`
 *   means always visible (no gating).
 */
data class FeatureEntry(
    override val route: String,
    val title: String,
    val requiredPermission: String? = null,
) : NavRoute

/**
 * Implemented by each feature module to declare the destinations it adds to the
 * app shell. The shell aggregates every contribution (typically via a Hilt
 * multibinding at the app level in a later work package) and applies permission
 * gating per [FeatureEntry.requiredPermission].
 */
interface ModuleNavContribution {
    /** The destinations this module contributes. */
    val entries: List<FeatureEntry>
}
