package eu.grandmedical.diten.mobile.feature.offermanagement.domain

/**
 * The HCM offer-readiness state, mirroring the backend `OfferReadinessState` enum.
 *
 * The backend has NO `JsonStringEnumConverter`, so on the wire every state field
 * is an **integer** ([code]). This enum is the domain-side representation; the
 * data layer maps [code] <-> [ReadinessState] at the DTO boundary via [fromCode].
 *
 * The int codes are fixed by the OfferManagement backend and must never be
 * reordered. NOTE: this module's ordering differs from the applicant-intake
 * pilot's — the measured `OfferReadinessState` enum is:
 * `Draft=0, Ready=1, Deferred=2, Blocked=3, NotRequired=4, Archived=5`.
 */
enum class ReadinessState(val code: Int) {
    Draft(0),
    Ready(1),
    Deferred(2),
    Blocked(3),
    NotRequired(4),
    Archived(5),
    ;

    companion object {
        /**
         * Maps a wire [code] to its [ReadinessState]. An unknown/out-of-range code
         * falls back to [Draft] (the safe, lowest-privilege default) rather than
         * throwing, so a backend that adds a new state can never crash the client.
         */
        fun fromCode(code: Int): ReadinessState = entries.firstOrNull { it.code == code } ?: Draft
    }
}
