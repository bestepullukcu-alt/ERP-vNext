package eu.grandmedical.diten.mobile.feature.candidatepipeline.data.mapper

import eu.grandmedical.diten.mobile.core.database.ScopeKeys
import eu.grandmedical.diten.mobile.core.database.entity.SyncStatus
import eu.grandmedical.diten.mobile.feature.candidatepipeline.data.dto.CandidatePipelineCreateRequestDto
import eu.grandmedical.diten.mobile.feature.candidatepipeline.data.dto.CandidatePipelineReadinessDto
import eu.grandmedical.diten.mobile.feature.candidatepipeline.data.dto.CandidatePipelineReadinessListItemDto
import eu.grandmedical.diten.mobile.feature.candidatepipeline.data.local.CandidatePipelineEntity
import eu.grandmedical.diten.mobile.feature.candidatepipeline.domain.CandidatePipelineListItem
import eu.grandmedical.diten.mobile.feature.candidatepipeline.domain.CandidatePipelineReadiness
import eu.grandmedical.diten.mobile.feature.candidatepipeline.domain.NewCandidatePipeline
import eu.grandmedical.diten.mobile.feature.candidatepipeline.domain.ReadinessState
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

fun CandidatePipelineReadinessDto.toDomain(syncStatus: SyncStatus = SyncStatus.SYNCED): CandidatePipelineReadiness =
    CandidatePipelineReadiness(
        id = id,
        code = code,
        displayName = displayName,
        pipelineReadinessState = ReadinessState.fromCode(pipelineReadinessState),
        pipelineStageGovernanceState = ReadinessState.fromCode(pipelineStageGovernanceState),
        interviewSchedulingReadinessState = ReadinessState.fromCode(interviewSchedulingReadinessState),
        interviewerAssignmentReadinessState = ReadinessState.fromCode(interviewerAssignmentReadinessState),
        evaluationGovernanceState = ReadinessState.fromCode(evaluationGovernanceState),
        candidateCommunicationBoundaryState = ReadinessState.fromCode(candidateCommunicationBoundaryState),
        consentPreconditionState = ReadinessState.fromCode(consentPreconditionState),
        dataMinimizationState = ReadinessState.fromCode(dataMinimizationState),
        retentionPolicyState = ReadinessState.fromCode(retentionPolicyState),
        evidencePolicyState = ReadinessState.fromCode(evidencePolicyState),
        calendarDependencyState = ReadinessState.fromCode(calendarDependencyState),
        notificationDependencyState = ReadinessState.fromCode(notificationDependencyState),
        documentDependencyState = ReadinessState.fromCode(documentDependencyState),
        automatedDecisionBoundaryState = ReadinessState.fromCode(automatedDecisionBoundaryState),
        dependencyStates = dependencyStates.mapValues { ReadinessState.fromCode(it.value) },
        sourceContractVersion = sourceContractVersion,
        pipelineReadinessVersion = pipelineReadinessVersion,
        deferredReason = deferredReason,
        lastEvaluatedAt = parseInstant(lastEvaluatedAt),
        syncStatus = syncStatus,
    )

// --- domain (new) -> create request DTO ---------------------------------------

fun NewCandidatePipeline.toCreateRequest(): CandidatePipelineCreateRequestDto =
    CandidatePipelineCreateRequestDto(
        code = code,
        displayName = displayName,
        pipelineReadinessState = pipelineReadinessState.code,
        pipelineStageGovernanceState = pipelineStageGovernanceState.code,
        interviewSchedulingReadinessState = interviewSchedulingReadinessState.code,
        interviewerAssignmentReadinessState = interviewerAssignmentReadinessState.code,
        evaluationGovernanceState = evaluationGovernanceState.code,
        candidateCommunicationBoundaryState = candidateCommunicationBoundaryState.code,
        consentPreconditionState = consentPreconditionState.code,
        dataMinimizationState = dataMinimizationState.code,
        retentionPolicyState = retentionPolicyState.code,
        evidencePolicyState = evidencePolicyState.code,
        calendarDependencyState = calendarDependencyState.code,
        notificationDependencyState = notificationDependencyState.code,
        documentDependencyState = documentDependencyState.code,
        automatedDecisionBoundaryState = automatedDecisionBoundaryState.code,
        dependencyStates = dependencyStates.mapValues { it.value.code },
        sourceContractVersion = sourceContractVersion,
        pipelineReadinessVersion = pipelineReadinessVersion,
        deferredReason = deferredReason,
    )

// --- domain (new) -> entity (local PENDING write) -----------------------------

@Suppress("LongParameterList")
fun NewCandidatePipeline.toEntity(
    id: String,
    scope: ScopeKeys,
    syncStatus: SyncStatus = SyncStatus.PENDING,
): CandidatePipelineEntity =
    CandidatePipelineEntity(
        id = id,
        scope = scope,
        code = code,
        displayName = displayName,
        pipelineReadinessState = pipelineReadinessState.code,
        pipelineStageGovernanceState = pipelineStageGovernanceState.code,
        interviewSchedulingReadinessState = interviewSchedulingReadinessState.code,
        interviewerAssignmentReadinessState = interviewerAssignmentReadinessState.code,
        evaluationGovernanceState = evaluationGovernanceState.code,
        candidateCommunicationBoundaryState = candidateCommunicationBoundaryState.code,
        consentPreconditionState = consentPreconditionState.code,
        dataMinimizationState = dataMinimizationState.code,
        retentionPolicyState = retentionPolicyState.code,
        evidencePolicyState = evidencePolicyState.code,
        calendarDependencyState = calendarDependencyState.code,
        notificationDependencyState = notificationDependencyState.code,
        documentDependencyState = documentDependencyState.code,
        automatedDecisionBoundaryState = automatedDecisionBoundaryState.code,
        dependencyStates = dependencyStates.mapValues { it.value.code },
        sourceContractVersion = sourceContractVersion,
        pipelineReadinessVersion = pipelineReadinessVersion,
        deferredReason = deferredReason,
        lastEvaluatedAt = null,
        syncStatus = syncStatus,
    )

/** Entity (a PENDING local row) -> create request, for the sync push. */
fun CandidatePipelineEntity.toCreateRequest(): CandidatePipelineCreateRequestDto =
    CandidatePipelineCreateRequestDto(
        code = code,
        displayName = displayName,
        pipelineReadinessState = pipelineReadinessState,
        pipelineStageGovernanceState = pipelineStageGovernanceState,
        interviewSchedulingReadinessState = interviewSchedulingReadinessState,
        interviewerAssignmentReadinessState = interviewerAssignmentReadinessState,
        evaluationGovernanceState = evaluationGovernanceState,
        candidateCommunicationBoundaryState = candidateCommunicationBoundaryState,
        consentPreconditionState = consentPreconditionState,
        dataMinimizationState = dataMinimizationState,
        retentionPolicyState = retentionPolicyState,
        evidencePolicyState = evidencePolicyState,
        calendarDependencyState = calendarDependencyState,
        notificationDependencyState = notificationDependencyState,
        documentDependencyState = documentDependencyState,
        automatedDecisionBoundaryState = automatedDecisionBoundaryState,
        dependencyStates = dependencyStates,
        sourceContractVersion = sourceContractVersion,
        pipelineReadinessVersion = pipelineReadinessVersion,
        deferredReason = deferredReason,
    )

// --- DTO -> entity (server row cached as SYNCED) ------------------------------

fun CandidatePipelineReadinessDto.toEntity(scope: ScopeKeys): CandidatePipelineEntity =
    CandidatePipelineEntity(
        id = id,
        scope = scope,
        code = code,
        displayName = displayName,
        pipelineReadinessState = pipelineReadinessState,
        pipelineStageGovernanceState = pipelineStageGovernanceState,
        interviewSchedulingReadinessState = interviewSchedulingReadinessState,
        interviewerAssignmentReadinessState = interviewerAssignmentReadinessState,
        evaluationGovernanceState = evaluationGovernanceState,
        candidateCommunicationBoundaryState = candidateCommunicationBoundaryState,
        consentPreconditionState = consentPreconditionState,
        dataMinimizationState = dataMinimizationState,
        retentionPolicyState = retentionPolicyState,
        evidencePolicyState = evidencePolicyState,
        calendarDependencyState = calendarDependencyState,
        notificationDependencyState = notificationDependencyState,
        documentDependencyState = documentDependencyState,
        automatedDecisionBoundaryState = automatedDecisionBoundaryState,
        dependencyStates = dependencyStates,
        sourceContractVersion = sourceContractVersion,
        pipelineReadinessVersion = pipelineReadinessVersion,
        deferredReason = deferredReason,
        lastEvaluatedAt = parseInstant(lastEvaluatedAt),
        syncStatus = SyncStatus.SYNCED,
    )

/**
 * List item DTO -> entity. The list payload omits most facet states, so those
 * columns are seeded with backend defaults; a later detail fetch/evaluate fills
 * in the authoritative values.
 */
fun CandidatePipelineReadinessListItemDto.toEntity(scope: ScopeKeys): CandidatePipelineEntity =
    CandidatePipelineEntity(
        id = id,
        scope = scope,
        code = code,
        displayName = displayName,
        pipelineReadinessState = pipelineReadinessState,
        pipelineStageGovernanceState = pipelineStageGovernanceState,
        interviewSchedulingReadinessState = interviewSchedulingReadinessState,
        interviewerAssignmentReadinessState = interviewerAssignmentReadinessState,
        evaluationGovernanceState = evaluationGovernanceState,
        candidateCommunicationBoundaryState = ReadinessState.Blocked.code,
        consentPreconditionState = ReadinessState.Deferred.code,
        dataMinimizationState = ReadinessState.Deferred.code,
        retentionPolicyState = ReadinessState.Deferred.code,
        evidencePolicyState = ReadinessState.Deferred.code,
        calendarDependencyState = ReadinessState.Deferred.code,
        notificationDependencyState = ReadinessState.Deferred.code,
        documentDependencyState = ReadinessState.Deferred.code,
        automatedDecisionBoundaryState = ReadinessState.Blocked.code,
        dependencyStates = emptyMap(),
        sourceContractVersion = sourceContractVersion,
        pipelineReadinessVersion = 1L,
        deferredReason = null,
        lastEvaluatedAt = parseInstant(lastEvaluatedAt),
        syncStatus = SyncStatus.SYNCED,
    )

// --- entity -> domain ---------------------------------------------------------

fun CandidatePipelineEntity.toDomain(): CandidatePipelineReadiness =
    CandidatePipelineReadiness(
        id = id,
        code = code,
        displayName = displayName,
        pipelineReadinessState = ReadinessState.fromCode(pipelineReadinessState),
        pipelineStageGovernanceState = ReadinessState.fromCode(pipelineStageGovernanceState),
        interviewSchedulingReadinessState = ReadinessState.fromCode(interviewSchedulingReadinessState),
        interviewerAssignmentReadinessState = ReadinessState.fromCode(interviewerAssignmentReadinessState),
        evaluationGovernanceState = ReadinessState.fromCode(evaluationGovernanceState),
        candidateCommunicationBoundaryState = ReadinessState.fromCode(candidateCommunicationBoundaryState),
        consentPreconditionState = ReadinessState.fromCode(consentPreconditionState),
        dataMinimizationState = ReadinessState.fromCode(dataMinimizationState),
        retentionPolicyState = ReadinessState.fromCode(retentionPolicyState),
        evidencePolicyState = ReadinessState.fromCode(evidencePolicyState),
        calendarDependencyState = ReadinessState.fromCode(calendarDependencyState),
        notificationDependencyState = ReadinessState.fromCode(notificationDependencyState),
        documentDependencyState = ReadinessState.fromCode(documentDependencyState),
        automatedDecisionBoundaryState = ReadinessState.fromCode(automatedDecisionBoundaryState),
        dependencyStates = dependencyStates.mapValues { ReadinessState.fromCode(it.value) },
        sourceContractVersion = sourceContractVersion,
        pipelineReadinessVersion = pipelineReadinessVersion,
        deferredReason = deferredReason,
        lastEvaluatedAt = lastEvaluatedAt,
        syncStatus = syncStatus,
    )

fun CandidatePipelineEntity.toListItem(): CandidatePipelineListItem =
    CandidatePipelineListItem(
        id = id,
        code = code,
        displayName = displayName,
        pipelineReadinessState = ReadinessState.fromCode(pipelineReadinessState),
        sourceContractVersion = sourceContractVersion,
        lastEvaluatedAt = lastEvaluatedAt,
        syncStatus = syncStatus,
    )
