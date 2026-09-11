package eu.grandmedical.diten.mobile.feature.performancereviews.data.mapper

import eu.grandmedical.diten.mobile.core.database.ScopeKeys
import eu.grandmedical.diten.mobile.core.database.entity.SyncStatus
import eu.grandmedical.diten.mobile.feature.performancereviews.data.dto.PerformanceReviewsCreateRequestDto
import eu.grandmedical.diten.mobile.feature.performancereviews.data.dto.PerformanceReviewsReadinessDto
import eu.grandmedical.diten.mobile.feature.performancereviews.data.dto.PerformanceReviewsReadinessListItemDto
import eu.grandmedical.diten.mobile.feature.performancereviews.data.local.PerformanceReviewsEntity
import eu.grandmedical.diten.mobile.feature.performancereviews.domain.NewPerformanceReviews
import eu.grandmedical.diten.mobile.feature.performancereviews.domain.PerformanceReviewsListItem
import eu.grandmedical.diten.mobile.feature.performancereviews.domain.PerformanceReviewsReadiness
import eu.grandmedical.diten.mobile.feature.performancereviews.domain.ReadinessState
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

fun PerformanceReviewsReadinessDto.toDomain(syncStatus: SyncStatus = SyncStatus.SYNCED): PerformanceReviewsReadiness =
    PerformanceReviewsReadiness(
        id = id,
        code = code,
        displayName = displayName,
        performanceReviewReadinessState = ReadinessState.fromCode(performanceReviewReadinessState),
        reviewCycleBoundaryState = ReadinessState.fromCode(reviewCycleBoundaryState),
        goalDependencyState = ReadinessState.fromCode(goalDependencyState),
        scoringBoundaryState = ReadinessState.fromCode(scoringBoundaryState),
        ratingBoundaryState = ReadinessState.fromCode(ratingBoundaryState),
        calibrationBoundaryState = ReadinessState.fromCode(calibrationBoundaryState),
        rankingBoundaryState = ReadinessState.fromCode(rankingBoundaryState),
        automatedDecisionBoundaryState = ReadinessState.fromCode(automatedDecisionBoundaryState),
        managerReviewUxBoundaryState = ReadinessState.fromCode(managerReviewUxBoundaryState),
        employeeReviewUxBoundaryState = ReadinessState.fromCode(employeeReviewUxBoundaryState),
        compensationDataBoundaryState = ReadinessState.fromCode(compensationDataBoundaryState),
        benefitsDataBoundaryState = ReadinessState.fromCode(benefitsDataBoundaryState),
        payrollDataBoundaryState = ReadinessState.fromCode(payrollDataBoundaryState),
        documentDependencyState = ReadinessState.fromCode(documentDependencyState),
        notificationDependencyState = ReadinessState.fromCode(notificationDependencyState),
        consentPreconditionState = ReadinessState.fromCode(consentPreconditionState),
        dataMinimizationState = ReadinessState.fromCode(dataMinimizationState),
        retentionPolicyState = ReadinessState.fromCode(retentionPolicyState),
        evidencePolicyState = ReadinessState.fromCode(evidencePolicyState),
        dependencyStates = dependencyStates.mapValues { ReadinessState.fromCode(it.value) },
        sourceContractVersion = sourceContractVersion,
        performanceReviewReadinessVersion = performanceReviewReadinessVersion,
        deferredReason = deferredReason,
        lastEvaluatedAt = parseInstant(lastEvaluatedAt),
        syncStatus = syncStatus,
    )

// --- domain (new) -> create request DTO ---------------------------------------

fun NewPerformanceReviews.toCreateRequest(): PerformanceReviewsCreateRequestDto =
    PerformanceReviewsCreateRequestDto(
        code = code,
        displayName = displayName,
        performanceReviewReadinessState = performanceReviewReadinessState.code,
        reviewCycleBoundaryState = reviewCycleBoundaryState.code,
        goalDependencyState = goalDependencyState.code,
        scoringBoundaryState = scoringBoundaryState.code,
        ratingBoundaryState = ratingBoundaryState.code,
        calibrationBoundaryState = calibrationBoundaryState.code,
        rankingBoundaryState = rankingBoundaryState.code,
        automatedDecisionBoundaryState = automatedDecisionBoundaryState.code,
        managerReviewUxBoundaryState = managerReviewUxBoundaryState.code,
        employeeReviewUxBoundaryState = employeeReviewUxBoundaryState.code,
        compensationDataBoundaryState = compensationDataBoundaryState.code,
        benefitsDataBoundaryState = benefitsDataBoundaryState.code,
        payrollDataBoundaryState = payrollDataBoundaryState.code,
        documentDependencyState = documentDependencyState.code,
        notificationDependencyState = notificationDependencyState.code,
        consentPreconditionState = consentPreconditionState.code,
        dataMinimizationState = dataMinimizationState.code,
        retentionPolicyState = retentionPolicyState.code,
        evidencePolicyState = evidencePolicyState.code,
        dependencyStates = dependencyStates.mapValues { it.value.code },
        sourceContractVersion = sourceContractVersion,
        performanceReviewReadinessVersion = performanceReviewReadinessVersion,
        deferredReason = deferredReason,
    )

// --- domain (new) -> entity (local PENDING write) -----------------------------

@Suppress("LongParameterList")
fun NewPerformanceReviews.toEntity(
    id: String,
    scope: ScopeKeys,
    syncStatus: SyncStatus = SyncStatus.PENDING,
): PerformanceReviewsEntity =
    PerformanceReviewsEntity(
        id = id,
        scope = scope,
        code = code,
        displayName = displayName,
        performanceReviewReadinessState = performanceReviewReadinessState.code,
        reviewCycleBoundaryState = reviewCycleBoundaryState.code,
        goalDependencyState = goalDependencyState.code,
        scoringBoundaryState = scoringBoundaryState.code,
        ratingBoundaryState = ratingBoundaryState.code,
        calibrationBoundaryState = calibrationBoundaryState.code,
        rankingBoundaryState = rankingBoundaryState.code,
        automatedDecisionBoundaryState = automatedDecisionBoundaryState.code,
        managerReviewUxBoundaryState = managerReviewUxBoundaryState.code,
        employeeReviewUxBoundaryState = employeeReviewUxBoundaryState.code,
        compensationDataBoundaryState = compensationDataBoundaryState.code,
        benefitsDataBoundaryState = benefitsDataBoundaryState.code,
        payrollDataBoundaryState = payrollDataBoundaryState.code,
        documentDependencyState = documentDependencyState.code,
        notificationDependencyState = notificationDependencyState.code,
        consentPreconditionState = consentPreconditionState.code,
        dataMinimizationState = dataMinimizationState.code,
        retentionPolicyState = retentionPolicyState.code,
        evidencePolicyState = evidencePolicyState.code,
        dependencyStates = dependencyStates.mapValues { it.value.code },
        sourceContractVersion = sourceContractVersion,
        performanceReviewReadinessVersion = performanceReviewReadinessVersion,
        deferredReason = deferredReason,
        lastEvaluatedAt = null,
        syncStatus = syncStatus,
    )

/** Entity (a PENDING local row) -> create request, for the sync push. */
fun PerformanceReviewsEntity.toCreateRequest(): PerformanceReviewsCreateRequestDto =
    PerformanceReviewsCreateRequestDto(
        code = code,
        displayName = displayName,
        performanceReviewReadinessState = performanceReviewReadinessState,
        reviewCycleBoundaryState = reviewCycleBoundaryState,
        goalDependencyState = goalDependencyState,
        scoringBoundaryState = scoringBoundaryState,
        ratingBoundaryState = ratingBoundaryState,
        calibrationBoundaryState = calibrationBoundaryState,
        rankingBoundaryState = rankingBoundaryState,
        automatedDecisionBoundaryState = automatedDecisionBoundaryState,
        managerReviewUxBoundaryState = managerReviewUxBoundaryState,
        employeeReviewUxBoundaryState = employeeReviewUxBoundaryState,
        compensationDataBoundaryState = compensationDataBoundaryState,
        benefitsDataBoundaryState = benefitsDataBoundaryState,
        payrollDataBoundaryState = payrollDataBoundaryState,
        documentDependencyState = documentDependencyState,
        notificationDependencyState = notificationDependencyState,
        consentPreconditionState = consentPreconditionState,
        dataMinimizationState = dataMinimizationState,
        retentionPolicyState = retentionPolicyState,
        evidencePolicyState = evidencePolicyState,
        dependencyStates = dependencyStates,
        sourceContractVersion = sourceContractVersion,
        performanceReviewReadinessVersion = performanceReviewReadinessVersion,
        deferredReason = deferredReason,
    )

// --- DTO -> entity (server row cached as SYNCED) ------------------------------

fun PerformanceReviewsReadinessDto.toEntity(scope: ScopeKeys): PerformanceReviewsEntity =
    PerformanceReviewsEntity(
        id = id,
        scope = scope,
        code = code,
        displayName = displayName,
        performanceReviewReadinessState = performanceReviewReadinessState,
        reviewCycleBoundaryState = reviewCycleBoundaryState,
        goalDependencyState = goalDependencyState,
        scoringBoundaryState = scoringBoundaryState,
        ratingBoundaryState = ratingBoundaryState,
        calibrationBoundaryState = calibrationBoundaryState,
        rankingBoundaryState = rankingBoundaryState,
        automatedDecisionBoundaryState = automatedDecisionBoundaryState,
        managerReviewUxBoundaryState = managerReviewUxBoundaryState,
        employeeReviewUxBoundaryState = employeeReviewUxBoundaryState,
        compensationDataBoundaryState = compensationDataBoundaryState,
        benefitsDataBoundaryState = benefitsDataBoundaryState,
        payrollDataBoundaryState = payrollDataBoundaryState,
        documentDependencyState = documentDependencyState,
        notificationDependencyState = notificationDependencyState,
        consentPreconditionState = consentPreconditionState,
        dataMinimizationState = dataMinimizationState,
        retentionPolicyState = retentionPolicyState,
        evidencePolicyState = evidencePolicyState,
        dependencyStates = dependencyStates,
        sourceContractVersion = sourceContractVersion,
        performanceReviewReadinessVersion = performanceReviewReadinessVersion,
        deferredReason = deferredReason,
        lastEvaluatedAt = parseInstant(lastEvaluatedAt),
        syncStatus = SyncStatus.SYNCED,
    )

/**
 * List item DTO -> entity. The list payload omits most facet states, so those
 * columns are seeded with backend defaults; a later detail fetch/evaluate fills
 * in the authoritative values.
 */
fun PerformanceReviewsReadinessListItemDto.toEntity(scope: ScopeKeys): PerformanceReviewsEntity =
    PerformanceReviewsEntity(
        id = id,
        scope = scope,
        code = code,
        displayName = displayName,
        performanceReviewReadinessState = performanceReviewReadinessState,
        reviewCycleBoundaryState = reviewCycleBoundaryState,
        goalDependencyState = ReadinessState.Deferred.code,
        scoringBoundaryState = scoringBoundaryState,
        ratingBoundaryState = ReadinessState.Blocked.code,
        calibrationBoundaryState = ReadinessState.Blocked.code,
        rankingBoundaryState = ReadinessState.Blocked.code,
        automatedDecisionBoundaryState = automatedDecisionBoundaryState,
        managerReviewUxBoundaryState = ReadinessState.Blocked.code,
        employeeReviewUxBoundaryState = ReadinessState.Blocked.code,
        compensationDataBoundaryState = ReadinessState.Blocked.code,
        benefitsDataBoundaryState = ReadinessState.Blocked.code,
        payrollDataBoundaryState = ReadinessState.Blocked.code,
        documentDependencyState = ReadinessState.Deferred.code,
        notificationDependencyState = ReadinessState.Deferred.code,
        consentPreconditionState = ReadinessState.Deferred.code,
        dataMinimizationState = ReadinessState.Deferred.code,
        retentionPolicyState = ReadinessState.Deferred.code,
        evidencePolicyState = ReadinessState.Deferred.code,
        dependencyStates = emptyMap(),
        sourceContractVersion = sourceContractVersion,
        performanceReviewReadinessVersion = 1L,
        deferredReason = null,
        lastEvaluatedAt = parseInstant(lastEvaluatedAt),
        syncStatus = SyncStatus.SYNCED,
    )

// --- entity -> domain ---------------------------------------------------------

fun PerformanceReviewsEntity.toDomain(): PerformanceReviewsReadiness =
    PerformanceReviewsReadiness(
        id = id,
        code = code,
        displayName = displayName,
        performanceReviewReadinessState = ReadinessState.fromCode(performanceReviewReadinessState),
        reviewCycleBoundaryState = ReadinessState.fromCode(reviewCycleBoundaryState),
        goalDependencyState = ReadinessState.fromCode(goalDependencyState),
        scoringBoundaryState = ReadinessState.fromCode(scoringBoundaryState),
        ratingBoundaryState = ReadinessState.fromCode(ratingBoundaryState),
        calibrationBoundaryState = ReadinessState.fromCode(calibrationBoundaryState),
        rankingBoundaryState = ReadinessState.fromCode(rankingBoundaryState),
        automatedDecisionBoundaryState = ReadinessState.fromCode(automatedDecisionBoundaryState),
        managerReviewUxBoundaryState = ReadinessState.fromCode(managerReviewUxBoundaryState),
        employeeReviewUxBoundaryState = ReadinessState.fromCode(employeeReviewUxBoundaryState),
        compensationDataBoundaryState = ReadinessState.fromCode(compensationDataBoundaryState),
        benefitsDataBoundaryState = ReadinessState.fromCode(benefitsDataBoundaryState),
        payrollDataBoundaryState = ReadinessState.fromCode(payrollDataBoundaryState),
        documentDependencyState = ReadinessState.fromCode(documentDependencyState),
        notificationDependencyState = ReadinessState.fromCode(notificationDependencyState),
        consentPreconditionState = ReadinessState.fromCode(consentPreconditionState),
        dataMinimizationState = ReadinessState.fromCode(dataMinimizationState),
        retentionPolicyState = ReadinessState.fromCode(retentionPolicyState),
        evidencePolicyState = ReadinessState.fromCode(evidencePolicyState),
        dependencyStates = dependencyStates.mapValues { ReadinessState.fromCode(it.value) },
        sourceContractVersion = sourceContractVersion,
        performanceReviewReadinessVersion = performanceReviewReadinessVersion,
        deferredReason = deferredReason,
        lastEvaluatedAt = lastEvaluatedAt,
        syncStatus = syncStatus,
    )

fun PerformanceReviewsEntity.toListItem(): PerformanceReviewsListItem =
    PerformanceReviewsListItem(
        id = id,
        code = code,
        displayName = displayName,
        performanceReviewReadinessState = ReadinessState.fromCode(performanceReviewReadinessState),
        sourceContractVersion = sourceContractVersion,
        lastEvaluatedAt = lastEvaluatedAt,
        syncStatus = syncStatus,
    )
