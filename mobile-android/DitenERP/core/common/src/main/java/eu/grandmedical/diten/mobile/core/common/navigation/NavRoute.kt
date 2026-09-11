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
 * A single, Compose-free menu descriptor a feature module contributes to the app
 * shell. It is the "menu" half of the feature-plugin contract: a feature provides
 * one `@IntoSet FeatureEntry` (this) for the Home menu and one `@IntoSet`
 * `FeatureNavGraph` (in `:core:feature`, the "nav" half) for its Compose
 * destinations. The shell aggregates every contributed [FeatureEntry] via Hilt
 * multibinding, filters by [requiredPermission], and navigates to [route] on tap.
 *
 * @property key stable, unique feature key (e.g. `applicant-intake`); identifies
 *   the feature independently of its display text or route.
 * @property route the start-destination route key the menu navigates to.
 * @property title human-readable label for menus/app bars.
 * @property requiredPermission the resource a user must hold *some* permission on
 *   for this entry to be visible (evaluated by
 *   [eu.grandmedical.diten.mobile.core.common.permission.PermissionGate.hasResourceAccess]);
 *   `null` means always visible (no gating).
 * @property iconKey optional stable icon identifier a shell may map to a drawable
 *   or Compose `ImageVector`; `null` means no icon.
 */
data class FeatureEntry(
    val key: String,
    override val route: String,
    val title: String,
    val requiredPermission: String? = null,
    val iconKey: String? = null,
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
