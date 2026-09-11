package eu.grandmedical.diten.mobile.feature.applicantintake.domain

import eu.grandmedical.diten.mobile.core.database.entity.SyncStatus
import java.time.Instant

/**
 * Full domain model of an applicant-intake readiness record.
 *
 * Every state is a strongly-typed [ReadinessState] (never a raw int) so the rest
 * of the app is insulated from the backend's integer wire encoding. [syncStatus]
 * is LOCAL-only metadata (not part of the backend contract): it tells the UI
 * whether this record is server-authoritative ([SyncStatus.SYNCED]),
 * optimistic/in-flight ([SyncStatus.PENDING]) or needs attention
 * ([SyncStatus.FAILED]).
 */
@Suppress("LongParameterList") // Faithful mirror of the backend readiness aggregate.
data class ApplicantIntakeReadiness(
    val id: String,
    val code: String,
    val displayName: String,
    val intakeState: ReadinessState,
    val sourceChannelState: ReadinessState,
    val consentPreconditionState: ReadinessState,
    val dataMinimizationState: ReadinessState,
    val duplicateHandlingState: ReadinessState,
    val retentionPolicyState: ReadinessState,
    val evidencePolicyState: ReadinessState,
    val applicantIdentityBoundaryState: ReadinessState,
    val publicUxBoundaryState: ReadinessState,
    val documentDependencyState: ReadinessState,
    val notificationDependencyState: ReadinessState,
    val dependencyStates: Map<String, ReadinessState> = emptyMap(),
    val sourceContractVersion: String,
    val applicantIntakeVersion: Long,
    val deferredReason: String? = null,
    val lastEvaluatedAt: Instant? = null,
    val syncStatus: SyncStatus = SyncStatus.SYNCED,
)

/**
 * A new record to create. Facet defaults mirror the backend's own defaults
 * (Deferred for most facets, Blocked for the public-UX boundary, Draft intake).
 */
@Suppress("LongParameterList") // Create request mirrors the backend's full facet set.
data class NewApplicantIntake(
    val code: String,
    val displayName: String,
    val intakeState: ReadinessState = ReadinessState.Draft,
    val sourceChannelState: ReadinessState = ReadinessState.Deferred,
    val consentPreconditionState: ReadinessState = ReadinessState.Deferred,
    val dataMinimizationState: ReadinessState = ReadinessState.Deferred,
    val duplicateHandlingState: ReadinessState = ReadinessState.Deferred,
    val retentionPolicyState: ReadinessState = ReadinessState.Deferred,
    val evidencePolicyState: ReadinessState = ReadinessState.Deferred,
    val applicantIdentityBoundaryState: ReadinessState = ReadinessState.Deferred,
    val publicUxBoundaryState: ReadinessState = ReadinessState.Blocked,
    val documentDependencyState: ReadinessState = ReadinessState.Deferred,
    val notificationDependencyState: ReadinessState = ReadinessState.Deferred,
    val dependencyStates: Map<String, ReadinessState> = emptyMap(),
    val sourceContractVersion: String = DEFAULT_SOURCE_CONTRACT_VERSION,
    val applicantIntakeVersion: Long = 1L,
    val deferredReason: String? = null,
) {
    companion object {
        /**
         * The default source-contract version. Deliberately "v1" and NOT a
         * `x.y.z` string: the backend rejects version strings matching that
         * pattern (a learned marker guard).
         */
        const val DEFAULT_SOURCE_CONTRACT_VERSION = "v1"
    }
}
