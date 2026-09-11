package eu.grandmedical.diten.mobile.feature.applicantintake.data.mapper

import eu.grandmedical.diten.mobile.core.database.ScopeKeys
import eu.grandmedical.diten.mobile.core.database.entity.SyncStatus
import eu.grandmedical.diten.mobile.feature.applicantintake.data.dto.ApplicantIntakeCreateRequestDto
import eu.grandmedical.diten.mobile.feature.applicantintake.data.dto.ApplicantIntakeReadinessDto
import eu.grandmedical.diten.mobile.feature.applicantintake.data.dto.ApplicantIntakeReadinessListItemDto
import eu.grandmedical.diten.mobile.feature.applicantintake.data.local.ApplicantIntakeEntity
import eu.grandmedical.diten.mobile.feature.applicantintake.domain.ApplicantIntakeListItem
import eu.grandmedical.diten.mobile.feature.applicantintake.domain.ApplicantIntakeReadiness
import eu.grandmedical.diten.mobile.feature.applicantintake.domain.NewApplicantIntake
import eu.grandmedical.diten.mobile.feature.applicantintake.domain.ReadinessState
import java.time.Instant

/**
 * Pure mappers across the three representations (DTO / entity / domain), and the
 * one place the Int <-> [ReadinessState] wire encoding is applied. No side
 * effects, so they are trivially unit-testable on the JVM.
 */

/** Parses an ISO-8601 datetime; a null/blank/malformed value yields null (never throws). */
internal fun parseInstant(value: String?): Instant? =
    value?.takeIf { it.isNotBlank() }?.let { runCatching { Instant.parse(it) }.getOrNull() }

// --- DTO -> domain ------------------------------------------------------------

fun ApplicantIntakeReadinessDto.toDomain(syncStatus: SyncStatus = SyncStatus.SYNCED): ApplicantIntakeReadiness =
    ApplicantIntakeReadiness(
        id = id,
        code = code,
        displayName = displayName,
        intakeState = ReadinessState.fromCode(intakeState),
        sourceChannelState = ReadinessState.fromCode(sourceChannelState),
        consentPreconditionState = ReadinessState.fromCode(consentPreconditionState),
        dataMinimizationState = ReadinessState.fromCode(dataMinimizationState),
        duplicateHandlingState = ReadinessState.fromCode(duplicateHandlingState),
        retentionPolicyState = ReadinessState.fromCode(retentionPolicyState),
        evidencePolicyState = ReadinessState.fromCode(evidencePolicyState),
        applicantIdentityBoundaryState = ReadinessState.fromCode(applicantIdentityBoundaryState),
        publicUxBoundaryState = ReadinessState.fromCode(publicUxBoundaryState),
        documentDependencyState = ReadinessState.fromCode(documentDependencyState),
        notificationDependencyState = ReadinessState.fromCode(notificationDependencyState),
        dependencyStates = dependencyStates.mapValues { ReadinessState.fromCode(it.value) },
        sourceContractVersion = sourceContractVersion,
        applicantIntakeVersion = applicantIntakeVersion,
        deferredReason = deferredReason,
        lastEvaluatedAt = parseInstant(lastEvaluatedAt),
        syncStatus = syncStatus,
    )

// --- domain (new) -> create request DTO ---------------------------------------

fun NewApplicantIntake.toCreateRequest(): ApplicantIntakeCreateRequestDto =
    ApplicantIntakeCreateRequestDto(
        code = code,
        displayName = displayName,
        intakeState = intakeState.code,
        sourceChannelState = sourceChannelState.code,
        consentPreconditionState = consentPreconditionState.code,
        dataMinimizationState = dataMinimizationState.code,
        duplicateHandlingState = duplicateHandlingState.code,
        retentionPolicyState = retentionPolicyState.code,
        evidencePolicyState = evidencePolicyState.code,
        applicantIdentityBoundaryState = applicantIdentityBoundaryState.code,
        publicUxBoundaryState = publicUxBoundaryState.code,
        documentDependencyState = documentDependencyState.code,
        notificationDependencyState = notificationDependencyState.code,
        dependencyStates = dependencyStates.mapValues { it.value.code },
        sourceContractVersion = sourceContractVersion,
        applicantIntakeVersion = applicantIntakeVersion,
        deferredReason = deferredReason,
    )

// --- domain (new) -> entity (local PENDING write) -----------------------------

@Suppress("LongParameterList")
fun NewApplicantIntake.toEntity(
    id: String,
    scope: ScopeKeys,
    syncStatus: SyncStatus = SyncStatus.PENDING,
): ApplicantIntakeEntity =
    ApplicantIntakeEntity(
        id = id,
        scope = scope,
        code = code,
        displayName = displayName,
        intakeState = intakeState.code,
        sourceChannelState = sourceChannelState.code,
        consentPreconditionState = consentPreconditionState.code,
        dataMinimizationState = dataMinimizationState.code,
        duplicateHandlingState = duplicateHandlingState.code,
        retentionPolicyState = retentionPolicyState.code,
        evidencePolicyState = evidencePolicyState.code,
        applicantIdentityBoundaryState = applicantIdentityBoundaryState.code,
        publicUxBoundaryState = publicUxBoundaryState.code,
        documentDependencyState = documentDependencyState.code,
        notificationDependencyState = notificationDependencyState.code,
        dependencyStates = dependencyStates.mapValues { it.value.code },
        sourceContractVersion = sourceContractVersion,
        applicantIntakeVersion = applicantIntakeVersion,
        deferredReason = deferredReason,
        lastEvaluatedAt = null,
        syncStatus = syncStatus,
    )

/** Entity (a PENDING local row) -> create request, for the sync push. */
fun ApplicantIntakeEntity.toCreateRequest(): ApplicantIntakeCreateRequestDto =
    ApplicantIntakeCreateRequestDto(
        code = code,
        displayName = displayName,
        intakeState = intakeState,
        sourceChannelState = sourceChannelState,
        consentPreconditionState = consentPreconditionState,
        dataMinimizationState = dataMinimizationState,
        duplicateHandlingState = duplicateHandlingState,
        retentionPolicyState = retentionPolicyState,
        evidencePolicyState = evidencePolicyState,
        applicantIdentityBoundaryState = applicantIdentityBoundaryState,
        publicUxBoundaryState = publicUxBoundaryState,
        documentDependencyState = documentDependencyState,
        notificationDependencyState = notificationDependencyState,
        dependencyStates = dependencyStates,
        sourceContractVersion = sourceContractVersion,
        applicantIntakeVersion = applicantIntakeVersion,
        deferredReason = deferredReason,
    )

// --- DTO -> entity (server row cached as SYNCED) ------------------------------

fun ApplicantIntakeReadinessDto.toEntity(scope: ScopeKeys): ApplicantIntakeEntity =
    ApplicantIntakeEntity(
        id = id,
        scope = scope,
        code = code,
        displayName = displayName,
        intakeState = intakeState,
        sourceChannelState = sourceChannelState,
        consentPreconditionState = consentPreconditionState,
        dataMinimizationState = dataMinimizationState,
        duplicateHandlingState = duplicateHandlingState,
        retentionPolicyState = retentionPolicyState,
        evidencePolicyState = evidencePolicyState,
        applicantIdentityBoundaryState = applicantIdentityBoundaryState,
        publicUxBoundaryState = publicUxBoundaryState,
        documentDependencyState = documentDependencyState,
        notificationDependencyState = notificationDependencyState,
        dependencyStates = dependencyStates,
        sourceContractVersion = sourceContractVersion,
        applicantIntakeVersion = applicantIntakeVersion,
        deferredReason = deferredReason,
        lastEvaluatedAt = parseInstant(lastEvaluatedAt),
        syncStatus = SyncStatus.SYNCED,
    )

/**
 * List item DTO -> entity. The list payload omits most facet states, so those
 * columns are seeded with backend defaults; a later detail fetch/evaluate fills
 * in the authoritative values.
 */
fun ApplicantIntakeReadinessListItemDto.toEntity(scope: ScopeKeys): ApplicantIntakeEntity =
    ApplicantIntakeEntity(
        id = id,
        scope = scope,
        code = code,
        displayName = displayName,
        intakeState = intakeState,
        sourceChannelState = sourceChannelState,
        consentPreconditionState = consentPreconditionState,
        dataMinimizationState = dataMinimizationState,
        duplicateHandlingState = duplicateHandlingState,
        retentionPolicyState = ReadinessState.Deferred.code,
        evidencePolicyState = ReadinessState.Deferred.code,
        applicantIdentityBoundaryState = ReadinessState.Deferred.code,
        publicUxBoundaryState = ReadinessState.Blocked.code,
        documentDependencyState = ReadinessState.Deferred.code,
        notificationDependencyState = ReadinessState.Deferred.code,
        dependencyStates = emptyMap(),
        sourceContractVersion = sourceContractVersion,
        applicantIntakeVersion = 1L,
        deferredReason = null,
        lastEvaluatedAt = parseInstant(lastEvaluatedAt),
        syncStatus = SyncStatus.SYNCED,
    )

// --- entity -> domain ---------------------------------------------------------

fun ApplicantIntakeEntity.toDomain(): ApplicantIntakeReadiness =
    ApplicantIntakeReadiness(
        id = id,
        code = code,
        displayName = displayName,
        intakeState = ReadinessState.fromCode(intakeState),
        sourceChannelState = ReadinessState.fromCode(sourceChannelState),
        consentPreconditionState = ReadinessState.fromCode(consentPreconditionState),
        dataMinimizationState = ReadinessState.fromCode(dataMinimizationState),
        duplicateHandlingState = ReadinessState.fromCode(duplicateHandlingState),
        retentionPolicyState = ReadinessState.fromCode(retentionPolicyState),
        evidencePolicyState = ReadinessState.fromCode(evidencePolicyState),
        applicantIdentityBoundaryState = ReadinessState.fromCode(applicantIdentityBoundaryState),
        publicUxBoundaryState = ReadinessState.fromCode(publicUxBoundaryState),
        documentDependencyState = ReadinessState.fromCode(documentDependencyState),
        notificationDependencyState = ReadinessState.fromCode(notificationDependencyState),
        dependencyStates = dependencyStates.mapValues { ReadinessState.fromCode(it.value) },
        sourceContractVersion = sourceContractVersion,
        applicantIntakeVersion = applicantIntakeVersion,
        deferredReason = deferredReason,
        lastEvaluatedAt = lastEvaluatedAt,
        syncStatus = syncStatus,
    )

fun ApplicantIntakeEntity.toListItem(): ApplicantIntakeListItem =
    ApplicantIntakeListItem(
        id = id,
        code = code,
        displayName = displayName,
        intakeState = ReadinessState.fromCode(intakeState),
        sourceContractVersion = sourceContractVersion,
        lastEvaluatedAt = lastEvaluatedAt,
        syncStatus = syncStatus,
    )
