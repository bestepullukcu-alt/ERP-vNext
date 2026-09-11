package eu.grandmedical.diten.mobile.feature.learningtraining.data.mapper

import eu.grandmedical.diten.mobile.core.database.ScopeKeys
import eu.grandmedical.diten.mobile.core.database.entity.SyncStatus
import eu.grandmedical.diten.mobile.feature.learningtraining.data.dto.LearningTrainingCreateRequestDto
import eu.grandmedical.diten.mobile.feature.learningtraining.data.dto.LearningTrainingReadinessDto
import eu.grandmedical.diten.mobile.feature.learningtraining.data.dto.LearningTrainingReadinessListItemDto
import eu.grandmedical.diten.mobile.feature.learningtraining.data.local.LearningTrainingEntity
import eu.grandmedical.diten.mobile.feature.learningtraining.domain.LearningTrainingListItem
import eu.grandmedical.diten.mobile.feature.learningtraining.domain.LearningTrainingReadiness
import eu.grandmedical.diten.mobile.feature.learningtraining.domain.NewLearningTraining
import eu.grandmedical.diten.mobile.feature.learningtraining.domain.ReadinessState
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

fun LearningTrainingReadinessDto.toDomain(syncStatus: SyncStatus = SyncStatus.SYNCED): LearningTrainingReadiness =
    LearningTrainingReadiness(
        id = id,
        code = code,
        displayName = displayName,
        learningTrainingReadinessState = ReadinessState.fromCode(learningTrainingReadinessState),
        courseCatalogBoundaryState = ReadinessState.fromCode(courseCatalogBoundaryState),
        enrollmentWorkflowBoundaryState = ReadinessState.fromCode(enrollmentWorkflowBoundaryState),
        completionTrackingBoundaryState = ReadinessState.fromCode(completionTrackingBoundaryState),
        certificationBoundaryState = ReadinessState.fromCode(certificationBoundaryState),
        assessmentScoringBoundaryState = ReadinessState.fromCode(assessmentScoringBoundaryState),
        automatedDecisionBoundaryState = ReadinessState.fromCode(automatedDecisionBoundaryState),
        learningContentDependencyState = ReadinessState.fromCode(learningContentDependencyState),
        skillTaxonomyDependencyState = ReadinessState.fromCode(skillTaxonomyDependencyState),
        documentDependencyState = ReadinessState.fromCode(documentDependencyState),
        notificationDependencyState = ReadinessState.fromCode(notificationDependencyState),
        consentPreconditionState = ReadinessState.fromCode(consentPreconditionState),
        dataMinimizationState = ReadinessState.fromCode(dataMinimizationState),
        retentionPolicyState = ReadinessState.fromCode(retentionPolicyState),
        evidencePolicyState = ReadinessState.fromCode(evidencePolicyState),
        dependencyStates = dependencyStates.mapValues { ReadinessState.fromCode(it.value) },
        sourceContractVersion = sourceContractVersion,
        learningTrainingReadinessVersion = learningTrainingReadinessVersion,
        deferredReason = deferredReason,
        lastEvaluatedAt = parseInstant(lastEvaluatedAt),
        syncStatus = syncStatus,
    )

// --- domain (new) -> create request DTO ---------------------------------------

fun NewLearningTraining.toCreateRequest(): LearningTrainingCreateRequestDto =
    LearningTrainingCreateRequestDto(
        code = code,
        displayName = displayName,
        learningTrainingReadinessState = learningTrainingReadinessState.code,
        courseCatalogBoundaryState = courseCatalogBoundaryState.code,
        enrollmentWorkflowBoundaryState = enrollmentWorkflowBoundaryState.code,
        completionTrackingBoundaryState = completionTrackingBoundaryState.code,
        certificationBoundaryState = certificationBoundaryState.code,
        assessmentScoringBoundaryState = assessmentScoringBoundaryState.code,
        automatedDecisionBoundaryState = automatedDecisionBoundaryState.code,
        learningContentDependencyState = learningContentDependencyState.code,
        skillTaxonomyDependencyState = skillTaxonomyDependencyState.code,
        documentDependencyState = documentDependencyState.code,
        notificationDependencyState = notificationDependencyState.code,
        consentPreconditionState = consentPreconditionState.code,
        dataMinimizationState = dataMinimizationState.code,
        retentionPolicyState = retentionPolicyState.code,
        evidencePolicyState = evidencePolicyState.code,
        dependencyStates = dependencyStates.mapValues { it.value.code },
        sourceContractVersion = sourceContractVersion,
        learningTrainingReadinessVersion = learningTrainingReadinessVersion,
        deferredReason = deferredReason,
    )

// --- domain (new) -> entity (local PENDING write) -----------------------------

@Suppress("LongParameterList")
fun NewLearningTraining.toEntity(
    id: String,
    scope: ScopeKeys,
    syncStatus: SyncStatus = SyncStatus.PENDING,
): LearningTrainingEntity =
    LearningTrainingEntity(
        id = id,
        scope = scope,
        code = code,
        displayName = displayName,
        learningTrainingReadinessState = learningTrainingReadinessState.code,
        courseCatalogBoundaryState = courseCatalogBoundaryState.code,
        enrollmentWorkflowBoundaryState = enrollmentWorkflowBoundaryState.code,
        completionTrackingBoundaryState = completionTrackingBoundaryState.code,
        certificationBoundaryState = certificationBoundaryState.code,
        assessmentScoringBoundaryState = assessmentScoringBoundaryState.code,
        automatedDecisionBoundaryState = automatedDecisionBoundaryState.code,
        learningContentDependencyState = learningContentDependencyState.code,
        skillTaxonomyDependencyState = skillTaxonomyDependencyState.code,
        documentDependencyState = documentDependencyState.code,
        notificationDependencyState = notificationDependencyState.code,
        consentPreconditionState = consentPreconditionState.code,
        dataMinimizationState = dataMinimizationState.code,
        retentionPolicyState = retentionPolicyState.code,
        evidencePolicyState = evidencePolicyState.code,
        dependencyStates = dependencyStates.mapValues { it.value.code },
        sourceContractVersion = sourceContractVersion,
        learningTrainingReadinessVersion = learningTrainingReadinessVersion,
        deferredReason = deferredReason,
        lastEvaluatedAt = null,
        syncStatus = syncStatus,
    )

/** Entity (a PENDING local row) -> create request, for the sync push. */
fun LearningTrainingEntity.toCreateRequest(): LearningTrainingCreateRequestDto =
    LearningTrainingCreateRequestDto(
        code = code,
        displayName = displayName,
        learningTrainingReadinessState = learningTrainingReadinessState,
        courseCatalogBoundaryState = courseCatalogBoundaryState,
        enrollmentWorkflowBoundaryState = enrollmentWorkflowBoundaryState,
        completionTrackingBoundaryState = completionTrackingBoundaryState,
        certificationBoundaryState = certificationBoundaryState,
        assessmentScoringBoundaryState = assessmentScoringBoundaryState,
        automatedDecisionBoundaryState = automatedDecisionBoundaryState,
        learningContentDependencyState = learningContentDependencyState,
        skillTaxonomyDependencyState = skillTaxonomyDependencyState,
        documentDependencyState = documentDependencyState,
        notificationDependencyState = notificationDependencyState,
        consentPreconditionState = consentPreconditionState,
        dataMinimizationState = dataMinimizationState,
        retentionPolicyState = retentionPolicyState,
        evidencePolicyState = evidencePolicyState,
        dependencyStates = dependencyStates,
        sourceContractVersion = sourceContractVersion,
        learningTrainingReadinessVersion = learningTrainingReadinessVersion,
        deferredReason = deferredReason,
    )

// --- DTO -> entity (server row cached as SYNCED) ------------------------------

fun LearningTrainingReadinessDto.toEntity(scope: ScopeKeys): LearningTrainingEntity =
    LearningTrainingEntity(
        id = id,
        scope = scope,
        code = code,
        displayName = displayName,
        learningTrainingReadinessState = learningTrainingReadinessState,
        courseCatalogBoundaryState = courseCatalogBoundaryState,
        enrollmentWorkflowBoundaryState = enrollmentWorkflowBoundaryState,
        completionTrackingBoundaryState = completionTrackingBoundaryState,
        certificationBoundaryState = certificationBoundaryState,
        assessmentScoringBoundaryState = assessmentScoringBoundaryState,
        automatedDecisionBoundaryState = automatedDecisionBoundaryState,
        learningContentDependencyState = learningContentDependencyState,
        skillTaxonomyDependencyState = skillTaxonomyDependencyState,
        documentDependencyState = documentDependencyState,
        notificationDependencyState = notificationDependencyState,
        consentPreconditionState = consentPreconditionState,
        dataMinimizationState = dataMinimizationState,
        retentionPolicyState = retentionPolicyState,
        evidencePolicyState = evidencePolicyState,
        dependencyStates = dependencyStates,
        sourceContractVersion = sourceContractVersion,
        learningTrainingReadinessVersion = learningTrainingReadinessVersion,
        deferredReason = deferredReason,
        lastEvaluatedAt = parseInstant(lastEvaluatedAt),
        syncStatus = SyncStatus.SYNCED,
    )

/**
 * List item DTO -> entity. The list payload carries only the top-level state and
 * four facets, so the remaining columns are seeded with backend defaults
 * (Blocked=3 for the behaviour boundaries, Deferred=2 for the dependency/policy
 * facets); a later detail fetch/evaluate fills in the authoritative values.
 */
fun LearningTrainingReadinessListItemDto.toEntity(scope: ScopeKeys): LearningTrainingEntity =
    LearningTrainingEntity(
        id = id,
        scope = scope,
        code = code,
        displayName = displayName,
        learningTrainingReadinessState = learningTrainingReadinessState,
        courseCatalogBoundaryState = courseCatalogBoundaryState,
        enrollmentWorkflowBoundaryState = ReadinessState.Blocked.code,
        completionTrackingBoundaryState = ReadinessState.Blocked.code,
        certificationBoundaryState = ReadinessState.Blocked.code,
        assessmentScoringBoundaryState = assessmentScoringBoundaryState,
        automatedDecisionBoundaryState = automatedDecisionBoundaryState,
        learningContentDependencyState = ReadinessState.Deferred.code,
        skillTaxonomyDependencyState = skillTaxonomyDependencyState,
        documentDependencyState = ReadinessState.Deferred.code,
        notificationDependencyState = ReadinessState.Deferred.code,
        consentPreconditionState = ReadinessState.Deferred.code,
        dataMinimizationState = ReadinessState.Deferred.code,
        retentionPolicyState = ReadinessState.Deferred.code,
        evidencePolicyState = ReadinessState.Deferred.code,
        dependencyStates = emptyMap(),
        sourceContractVersion = sourceContractVersion,
        learningTrainingReadinessVersion = 1L,
        deferredReason = null,
        lastEvaluatedAt = parseInstant(lastEvaluatedAt),
        syncStatus = SyncStatus.SYNCED,
    )

// --- entity -> domain ---------------------------------------------------------

fun LearningTrainingEntity.toDomain(): LearningTrainingReadiness =
    LearningTrainingReadiness(
        id = id,
        code = code,
        displayName = displayName,
        learningTrainingReadinessState = ReadinessState.fromCode(learningTrainingReadinessState),
        courseCatalogBoundaryState = ReadinessState.fromCode(courseCatalogBoundaryState),
        enrollmentWorkflowBoundaryState = ReadinessState.fromCode(enrollmentWorkflowBoundaryState),
        completionTrackingBoundaryState = ReadinessState.fromCode(completionTrackingBoundaryState),
        certificationBoundaryState = ReadinessState.fromCode(certificationBoundaryState),
        assessmentScoringBoundaryState = ReadinessState.fromCode(assessmentScoringBoundaryState),
        automatedDecisionBoundaryState = ReadinessState.fromCode(automatedDecisionBoundaryState),
        learningContentDependencyState = ReadinessState.fromCode(learningContentDependencyState),
        skillTaxonomyDependencyState = ReadinessState.fromCode(skillTaxonomyDependencyState),
        documentDependencyState = ReadinessState.fromCode(documentDependencyState),
        notificationDependencyState = ReadinessState.fromCode(notificationDependencyState),
        consentPreconditionState = ReadinessState.fromCode(consentPreconditionState),
        dataMinimizationState = ReadinessState.fromCode(dataMinimizationState),
        retentionPolicyState = ReadinessState.fromCode(retentionPolicyState),
        evidencePolicyState = ReadinessState.fromCode(evidencePolicyState),
        dependencyStates = dependencyStates.mapValues { ReadinessState.fromCode(it.value) },
        sourceContractVersion = sourceContractVersion,
        learningTrainingReadinessVersion = learningTrainingReadinessVersion,
        deferredReason = deferredReason,
        lastEvaluatedAt = lastEvaluatedAt,
        syncStatus = syncStatus,
    )

fun LearningTrainingEntity.toListItem(): LearningTrainingListItem =
    LearningTrainingListItem(
        id = id,
        code = code,
        displayName = displayName,
        learningTrainingReadinessState = ReadinessState.fromCode(learningTrainingReadinessState),
        sourceContractVersion = sourceContractVersion,
        lastEvaluatedAt = lastEvaluatedAt,
        syncStatus = syncStatus,
    )
