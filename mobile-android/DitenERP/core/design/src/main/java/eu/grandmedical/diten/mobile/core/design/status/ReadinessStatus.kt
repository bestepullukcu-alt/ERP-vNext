package eu.grandmedical.diten.mobile.core.design.status

/**
 * Readiness state model shared across the ERP (associations operations, passports,
 * memberships, etc.). This file is intentionally PURE Kotlin — no Compose imports —
 * so the color/label decision can be unit-tested on the JVM without an emulator.
 *
 * Color resolution to a real Compose `Color` happens in the component layer via
 * [eu.grandmedical.diten.mobile.core.design.status.statusColor]; the decision here
 * yields a semantic [StatusToken] token instead.
 */

/**
 * Semantic color token for a readiness state. Maps to a concrete brand color in the
 * Compose layer, but stays framework-free so it is JVM-assertable.
 *
 * Convention:
 *  - Ready       -> Success (green)
 *  - Deferred    -> Warning (amber)
 *  - Draft       -> Secondary (gray)
 *  - Blocked     -> Danger  (red)
 *  - NotRequired -> Info    (cyan)
 *  - Archived    -> Muted   (muted/secondary gray)
 */
enum class StatusToken {
    Success,
    Warning,
    Secondary,
    Danger,
    Info,
    Muted,
}

/**
 * The canonical readiness states. [Unknown] is the safe fallback for any string that
 * does not match a known state.
 */
enum class ReadinessStatus(
    val label: String,
    val token: StatusToken,
) {
    Draft("Draft", StatusToken.Secondary),
    Deferred("Deferred", StatusToken.Warning),
    Ready("Ready", StatusToken.Success),
    Blocked("Blocked", StatusToken.Danger),
    NotRequired("Not Required", StatusToken.Info),
    Archived("Archived", StatusToken.Muted),
    Unknown("Unknown", StatusToken.Secondary),
    ;

    companion object {
        /**
         * Case-insensitive parse. Accepts the canonical names as well as common
         * spelling variants (e.g. "notrequired", "not_required", "not-required").
         * Returns [Unknown] for anything unrecognised (never throws).
         */
        fun from(state: String?): ReadinessStatus {
            val normalized = state
                ?.trim()
                ?.lowercase()
                ?.replace("_", "")
                ?.replace("-", "")
                ?.replace(" ", "")
                ?: return Unknown

            return when (normalized) {
                "draft" -> Draft
                "deferred" -> Deferred
                "ready" -> Ready
                "blocked" -> Blocked
                "notrequired" -> NotRequired
                "archived" -> Archived
                else -> Unknown
            }
        }
    }
}

/**
 * Pure, case-insensitive lookup of the semantic token for a readiness [state].
 * Safe fallback ([StatusToken.Secondary] via [ReadinessStatus.Unknown]) for unknown input.
 */
fun statusToken(state: String?): StatusToken = ReadinessStatus.from(state).token

/**
 * Pure, case-insensitive lookup of the human-readable label for a readiness [state].
 * Returns the canonical label, or "Unknown" for unrecognised input.
 */
fun statusLabel(state: String?): String = ReadinessStatus.from(state).label
