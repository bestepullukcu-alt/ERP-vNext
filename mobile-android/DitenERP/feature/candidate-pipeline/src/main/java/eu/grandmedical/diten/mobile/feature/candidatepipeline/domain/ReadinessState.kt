package eu.grandmedical.diten.mobile.feature.candidatepipeline.domain

/**
 * The HCM readiness state, mirroring the backend `CandidatePipelineReadinessState`
 * enum.
 *
 * The backend has NO `JsonStringEnumConverter`, so on the wire every state field
 * is an **integer** ([code]). This enum is the domain-side representation; the
 * data layer maps [code] <-> [ReadinessState] at the DTO boundary via [fromCode].
 *
 * The int codes are fixed by the backend and must never be reordered. NOTE: the
 * candidate-pipeline enum ORDERS `Ready` BEFORE `Deferred` — the OPPOSITE of the
 * applicant-intake pilot — so the wire codes are:
 * `Draft=0, Ready=1, Deferred=2, Blocked=3, NotRequired=4, Archived=5`.
 * (Measured directly from `Domain.Enums.CandidatePipelineReadinessState`.)
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
