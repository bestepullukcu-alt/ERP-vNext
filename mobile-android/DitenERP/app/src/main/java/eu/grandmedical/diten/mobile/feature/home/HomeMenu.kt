package eu.grandmedical.diten.mobile.feature.home

import eu.grandmedical.diten.mobile.core.common.navigation.FeatureEntry
import eu.grandmedical.diten.mobile.core.common.permission.PermissionGate

/**
 * Pure, dependency-free construction of the Home module menu from the set of
 * installed features.
 *
 * The shell no longer keeps a static module catalogue: the menu is built at
 * runtime from the Hilt-multibound `Set<FeatureEntry>` (every installed feature
 * contributes exactly one), filtered to what the signed-in user may see and
 * sorted stably. This keeps the logic trivially unit-testable — no Android,
 * Compose or Hilt types involved.
 */
object HomeMenu {

    /**
     * The entries a user holding [permissions] may see: an entry with no
     * [FeatureEntry.requiredPermission] is always visible; otherwise it is shown
     * only when the user holds *some* permission on that resource
     * ([PermissionGate.hasResourceAccess]). The result is sorted by [title] for a
     * stable ordering independent of Hilt's set iteration order.
     */
    fun visibleEntries(
        features: Set<FeatureEntry>,
        permissions: Set<String>,
    ): List<FeatureEntry> =
        features
            .filter { entry ->
                val required = entry.requiredPermission
                required == null || PermissionGate.hasResourceAccess(permissions, required)
            }
            .sortedBy { it.title }
}
