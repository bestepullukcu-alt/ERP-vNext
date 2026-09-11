package eu.grandmedical.diten.mobile.core.common.permission

/**
 * Pure, dependency-free evaluation of backend permission claims.
 *
 * Permissions are the strings carried in the JWT `permission` claims, shaped as
 * `<service>.<resource>.<action>` (e.g. `hcm.applicant-intake.read`, with actions
 * `read` / `evaluate` / `manage`). Claims contain **no wildcards** — the backend
 * enumerates every granted permission explicitly, so this gate never interprets
 * `*` or any pattern. Matching an action is therefore an exact (case-insensitive)
 * comparison; menu/module visibility uses a resource *prefix* check.
 *
 * The granted set is wired at the app level from `:core:auth` (the JWT source);
 * this object stays free of any auth/network dependency so it is trivially
 * unit-testable and reusable from any layer.
 */
object PermissionGate {

    /** True when [required] is present in [granted] as an exact, case-insensitive match. */
    fun isGranted(granted: Set<String>, required: String): Boolean =
        granted.any { it.equals(required, ignoreCase = true) }

    /** True when at least one of [required] is granted. */
    fun hasAny(granted: Set<String>, required: Collection<String>): Boolean =
        required.any { isGranted(granted, it) }

    /** True when every one of [required] is granted. */
    fun hasAll(granted: Set<String>, required: Collection<String>): Boolean =
        required.all { isGranted(granted, it) }

    /**
     * True when the user holds *any* permission on [resource] — i.e. some granted
     * string starts with `"<resource>."`. Used to decide whether a module/menu
     * entry is visible at all, regardless of which specific action is allowed.
     */
    fun hasResourceAccess(granted: Set<String>, resource: String): Boolean {
        val prefix = "$resource."
        return granted.any { it.startsWith(prefix, ignoreCase = true) }
    }
}
