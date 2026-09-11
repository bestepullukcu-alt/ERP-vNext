package eu.grandmedical.diten.mobile.feature.competencyskills.data.mapper

import eu.grandmedical.diten.mobile.core.database.ScopeKeys
import eu.grandmedical.diten.mobile.core.database.entity.SyncStatus
import eu.grandmedical.diten.mobile.feature.competencyskills.data.dto.CompetencySkillsCreateRequestDto
import eu.grandmedical.diten.mobile.feature.competencyskills.data.dto.CompetencySkillsReadinessDto
import eu.grandmedical.diten.mobile.feature.competencyskills.data.dto.CompetencySkillsReadinessListItemDto
import eu.grandmedical.diten.mobile.feature.competencyskills.data.local.CompetencySkillsEntity
import eu.grandmedical.diten.mobile.feature.competencyskills.domain.CompetencySkillsListItem
import eu.grandmedical.diten.mobile.feature.competencyskills.domain.CompetencySkillsReadiness
import eu.grandmedical.diten.mobile.feature.competencyskills.domain.NewCompetencySkills
import eu.grandmedical.diten.mobile.feature.competencyskills.domain.ReadinessState
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

fun CompetencySkillsReadinessDto.toDomain(syncStatus: SyncStatus = SyncStatus.SYNCED): CompetencySkillsReadiness =
    CompetencySkillsReadiness(
        id = id,
        code = code,
        displayName = displayName,
        competencySkillsReadinessState = ReadinessState.fromCode(competencySkillsReadinessState),
        assessmentWorkflowBoundaryState = ReadinessState.fromCode(assessmentWorkflowBoundaryState),
        competencyFrameworkDependencyState = ReadinessState.fromCode(competencyFrameworkDependencyState),
        skillTaxonomyDependencyState = ReadinessState.fromCode(skillTaxonomyDependencyState),
        skillScoringBoundaryState = ReadinessState.fromCode(skillScoringBoundaryState),
        ratingBoundaryState = ReadinessState.fromCode(ratingBoundaryState),
        calibrationBoundaryState = ReadinessState.fromCode(calibrationBoundaryState),
        rankingBoundaryState = ReadinessState.fromCode(rankingBoundaryState),
        automatedDecisionBoundaryState = ReadinessState.fromCode(automatedDecisionBoundaryState),
        managerAssessmentUxBoundaryState = ReadinessState.fromCode(managerAssessmentUxBoundaryState),
        employeeAssessmentUxBoundaryState = ReadinessState.fromCode(employeeAssessmentUxBoundaryState),
        documentDependencyState = ReadinessState.fromCode(documentDependencyState),
        notificationDependencyState = ReadinessState.fromCode(notificationDependencyState),
        consentPreconditionState = ReadinessState.fromCode(consentPreconditionState),
        dataMinimizationState = ReadinessState.fromCode(dataMinimizationState),
        retentionPolicyState = ReadinessState.fromCode(retentionPolicyState),
        evidencePolicyState = ReadinessState.fromCode(evidencePolicyState),
        dependencyStates = dependencyStates.mapValues { ReadinessState.fromCode(it.value) },
        sourceContractVersion = sourceContractVersion,
        competencySkillsReadinessVersion = competencySkillsReadinessVersion,
        deferredReason = deferredReason,
        lastEvaluatedAt = parseInstant(lastEvaluatedAt),
        syncStatus = syncStatus,
    )

// --- domain (new) -> create request DTO ---------------------------------------

fun NewCompetencySkills.toCreateRequest(): CompetencySkillsCreateRequestDto =
    CompetencySkillsCreateRequestDto(
        code = code,
        displayName = displayName,
        competencySkillsReadinessState = competencySkillsReadinessState.code,
        assessmentWorkflowBoundaryState = assessmentWorkflowBoundaryState.code,
        competencyFrameworkDependencyState = competencyFrameworkDependencyState.code,
        skillTaxonomyDependencyState = skillTaxonomyDependencyState.code,
        skillScoringBoundaryState = skillScoringBoundaryState.code,
        ratingBoundaryState = ratingBoundaryState.code,
        calibrationBoundaryState = calibrationBoundaryState.code,
        rankingBoundaryState = rankingBoundaryState.code,
        automatedDecisionBoundaryState = automatedDecisionBoundaryState.code,
        managerAssessmentUxBoundaryState = managerAssessmentUxBoundaryState.code,
        employeeAssessmentUxBoundaryState = employeeAssessmentUxBoundaryState.code,
        documentDependencyState = documentDependencyState.code,
        notificationDependencyState = notificationDependencyState.code,
        consentPreconditionState = consentPreconditionState.code,
        dataMinimizationState = dataMinimizationState.code,
        retentionPolicyState = retentionPolicyState.code,
        evidencePolicyState = evidencePolicyState.code,
        dependencyStates = dependencyStates.mapValues { it.value.code },
        sourceContractVersion = sourceContractVersion,
        competencySkillsReadinessVersion = competencySkillsReadinessVersion,
        deferredReason = deferredReason,
    )

// --- domain (new) -> entity (local PENDING write) -----------------------------

@Suppress("LongParameterList")
fun NewCompetencySkills.toEntity(
    id: String,
    scope: ScopeKeys,
    syncStatus: SyncStatus = SyncStatus.PENDING,
): CompetencySkillsEntity =
    CompetencySkillsEntity(
        id = id,
        scope = scope,
        code = code,
        displayName = displayName,
        competencySkillsReadinessState = competencySkillsReadinessState.code,
        assessmentWorkflowBoundaryState = assessmentWorkflowBoundaryState.code,
        competencyFrameworkDependencyState = competencyFrameworkDependencyState.code,
        skillTaxonomyDependencyState = skillTaxonomyDependencyState.code,
        skillScoringBoundaryState = skillScoringBoundaryState.code,
        ratingBoundaryState = ratingBoundaryState.code,
        calibrationBoundaryState = calibrationBoundaryState.code,
        rankingBoundaryState = rankingBoundaryState.code,
        automatedDecisionBoundaryState = automatedDecisionBoundaryState.code,
        managerAssessmentUxBoundaryState = managerAssessmentUxBoundaryState.code,
        employeeAssessmentUxBoundaryState = employeeAssessmentUxBoundaryState.code,
        documentDependencyState = documentDependencyState.code,
        notificationDependencyState = notificationDependencyState.code,
        consentPreconditionState = consentPreconditionState.code,
        dataMinimizationState = dataMinimizationState.code,
        retentionPolicyState = retentionPolicyState.code,
        evidencePolicyState = evidencePolicyState.code,
        dependencyStates = dependencyStates.mapValues { it.value.code },
        sourceContractVersion = sourceContractVersion,
        competencySkillsReadinessVersion = competencySkillsReadinessVersion,
        deferredReason = deferredReason,
        lastEvaluatedAt = null,
        syncStatus = syncStatus,
    )

/** Entity (a PENDING local row) -> create request, for the sync push. */
fun CompetencySkillsEntity.toCreateRequest(): CompetencySkillsCreateRequestDto =
    CompetencySkillsCreateRequestDto(
        code = code,
        displayName = displayName,
        competencySkillsReadinessState = competencySkillsReadinessState,
        assessmentWorkflowBoundaryState = assessmentWorkflowBoundaryState,
        competencyFrameworkDependencyState = competencyFrameworkDependencyState,
        skillTaxonomyDependencyState = skillTaxonomyDependencyState,
        skillScoringBoundaryState = skillScoringBoundaryState,
        ratingBoundaryState = ratingBoundaryState,
        calibrationBoundaryState = calibrationBoundaryState,
        rankingBoundaryState = rankingBoundaryState,
        automatedDecisionBoundaryState = automatedDecisionBoundaryState,
        managerAssessmentUxBoundaryState = managerAssessmentUxBoundaryState,
        employeeAssessmentUxBoundaryState = employeeAssessmentUxBoundaryState,
        documentDependencyState = documentDependencyState,
        notificationDependencyState = notificationDependencyState,
        consentPreconditionState = consentPreconditionState,
        dataMinimizationState = dataMinimizationState,
        retentionPolicyState = retentionPolicyState,
        evidencePolicyState = evidencePolicyState,
        dependencyStates = dependencyStates,
        sourceContractVersion = sourceContractVersion,
        competencySkillsReadinessVersion = competencySkillsReadinessVersion,
        deferredReason = deferredReason,
    )

// --- DTO -> entity (server row cached as SYNCED) ------------------------------

fun CompetencySkillsReadinessDto.toEntity(scope: ScopeKeys): CompetencySkillsEntity =
    CompetencySkillsEntity(
        id = id,
        scope = scope,
        code = code,
        displayName = displayName,
        competencySkillsReadinessState = competencySkillsReadinessState,
        assessmentWorkflowBoundaryState = assessmentWorkflowBoundaryState,
        competencyFrameworkDependencyState = competencyFrameworkDependencyState,
        skillTaxonomyDependencyState = skillTaxonomyDependencyState,
        skillScoringBoundaryState = skillScoringBoundaryState,
        ratingBoundaryState = ratingBoundaryState,
        calibrationBoundaryState = calibrationBoundaryState,
        rankingBoundaryState = rankingBoundaryState,
        automatedDecisionBoundaryState = automatedDecisionBoundaryState,
        managerAssessmentUxBoundaryState = managerAssessmentUxBoundaryState,
        employeeAssessmentUxBoundaryState = employeeAssessmentUxBoundaryState,
        documentDependencyState = documentDependencyState,
        notificationDependencyState = notificationDependencyState,
        consentPreconditionState = consentPreconditionState,
        dataMinimizationState = dataMinimizationState,
        retentionPolicyState = retentionPolicyState,
        evidencePolicyState = evidencePolicyState,
        dependencyStates = dependencyStates,
        sourceContractVersion = sourceContractVersion,
        competencySkillsReadinessVersion = competencySkillsReadinessVersion,
        deferredReason = deferredReason,
        lastEvaluatedAt = parseInstant(lastEvaluatedAt),
        syncStatus = SyncStatus.SYNCED,
    )

/**
 * List item DTO -> entity. The list payload omits most facet states, so those
 * columns are seeded with backend defaults; a later detail fetch/evaluate fills
 * in the authoritative values.
 */
fun CompetencySkillsReadinessListItemDto.toEntity(scope: ScopeKeys): CompetencySkillsEntity =
    CompetencySkillsEntity(
        id = id,
        scope = scope,
        code = code,
        displayName = displayName,
        competencySkillsReadinessState = competencySkillsReadinessState,
        assessmentWorkflowBoundaryState = assessmentWorkflowBoundaryState,
        competencyFrameworkDependencyState = ReadinessState.Deferred.code,
        skillTaxonomyDependencyState = skillTaxonomyDependencyState,
        skillScoringBoundaryState = skillScoringBoundaryState,
        ratingBoundaryState = ReadinessState.Blocked.code,
        calibrationBoundaryState = ReadinessState.Blocked.code,
        rankingBoundaryState = ReadinessState.Blocked.code,
        automatedDecisionBoundaryState = automatedDecisionBoundaryState,
        managerAssessmentUxBoundaryState = ReadinessState.Blocked.code,
        employeeAssessmentUxBoundaryState = ReadinessState.Blocked.code,
        documentDependencyState = ReadinessState.Deferred.code,
        notificationDependencyState = ReadinessState.Deferred.code,
        consentPreconditionState = ReadinessState.Deferred.code,
        dataMinimizationState = ReadinessState.Deferred.code,
        retentionPolicyState = ReadinessState.Deferred.code,
        evidencePolicyState = ReadinessState.Deferred.code,
        dependencyStates = emptyMap(),
        sourceContractVersion = sourceContractVersion,
        competencySkillsReadinessVersion = 1L,
        deferredReason = null,
        lastEvaluatedAt = parseInstant(lastEvaluatedAt),
        syncStatus = SyncStatus.SYNCED,
    )

// --- entity -> domain ---------------------------------------------------------

fun CompetencySkillsEntity.toDomain(): CompetencySkillsReadiness =
    CompetencySkillsReadiness(
        id = id,
        code = code,
        displayName = displayName,
        competencySkillsReadinessState = ReadinessState.fromCode(competencySkillsReadinessState),
        assessmentWorkflowBoundaryState = ReadinessState.fromCode(assessmentWorkflowBoundaryState),
        competencyFrameworkDependencyState = ReadinessState.fromCode(competencyFrameworkDependencyState),
        skillTaxonomyDependencyState = ReadinessState.fromCode(skillTaxonomyDependencyState),
        skillScoringBoundaryState = ReadinessState.fromCode(skillScoringBoundaryState),
        ratingBoundaryState = ReadinessState.fromCode(ratingBoundaryState),
        calibrationBoundaryState = ReadinessState.fromCode(calibrationBoundaryState),
        rankingBoundaryState = ReadinessState.fromCode(rankingBoundaryState),
        automatedDecisionBoundaryState = ReadinessState.fromCode(automatedDecisionBoundaryState),
        managerAssessmentUxBoundaryState = ReadinessState.fromCode(managerAssessmentUxBoundaryState),
        employeeAssessmentUxBoundaryState = ReadinessState.fromCode(employeeAssessmentUxBoundaryState),
        documentDependencyState = ReadinessState.fromCode(documentDependencyState),
        notificationDependencyState = ReadinessState.fromCode(notificationDependencyState),
        consentPreconditionState = ReadinessState.fromCode(consentPreconditionState),
        dataMinimizationState = ReadinessState.fromCode(dataMinimizationState),
        retentionPolicyState = ReadinessState.fromCode(retentionPolicyState),
        evidencePolicyState = ReadinessState.fromCode(evidencePolicyState),
        dependencyStates = dependencyStates.mapValues { ReadinessState.fromCode(it.value) },
        sourceContractVersion = sourceContractVersion,
        competencySkillsReadinessVersion = competencySkillsReadinessVersion,
        deferredReason = deferredReason,
        lastEvaluatedAt = lastEvaluatedAt,
        syncStatus = syncStatus,
    )

fun CompetencySkillsEntity.toListItem(): CompetencySkillsListItem =
    CompetencySkillsListItem(
        id = id,
        code = code,
        displayName = displayName,
        competencySkillsReadinessState = ReadinessState.fromCode(competencySkillsReadinessState),
        sourceContractVersion = sourceContractVersion,
        lastEvaluatedAt = lastEvaluatedAt,
        syncStatus = syncStatus,
    )
