package eu.grandmedical.diten.mobile.feature.compensationbenefits.data.mapper

import eu.grandmedical.diten.mobile.core.database.ScopeKeys
import eu.grandmedical.diten.mobile.core.database.entity.SyncStatus
import eu.grandmedical.diten.mobile.feature.compensationbenefits.data.dto.CompensationBenefitsCreateRequestDto
import eu.grandmedical.diten.mobile.feature.compensationbenefits.data.dto.CompensationBenefitsReadinessDto
import eu.grandmedical.diten.mobile.feature.compensationbenefits.data.dto.CompensationBenefitsReadinessListItemDto
import eu.grandmedical.diten.mobile.feature.compensationbenefits.data.local.CompensationBenefitsEntity
import eu.grandmedical.diten.mobile.feature.compensationbenefits.domain.CompensationBenefitsListItem
import eu.grandmedical.diten.mobile.feature.compensationbenefits.domain.CompensationBenefitsReadiness
import eu.grandmedical.diten.mobile.feature.compensationbenefits.domain.NewCompensationBenefits
import eu.grandmedical.diten.mobile.feature.compensationbenefits.domain.ReadinessState
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

fun CompensationBenefitsReadinessDto.toDomain(syncStatus: SyncStatus = SyncStatus.SYNCED): CompensationBenefitsReadiness =
    CompensationBenefitsReadiness(
        id = id,
        code = code,
        displayName = displayName,
        compensationBenefitsReadinessState = ReadinessState.fromCode(compensationBenefitsReadinessState),
        compensationPlanBoundaryState = ReadinessState.fromCode(compensationPlanBoundaryState),
        benefitProgramBoundaryState = ReadinessState.fromCode(benefitProgramBoundaryState),
        payGradeMappingBoundaryState = ReadinessState.fromCode(payGradeMappingBoundaryState),
        benefitEnrollmentBoundaryState = ReadinessState.fromCode(benefitEnrollmentBoundaryState),
        compensationReviewBoundaryState = ReadinessState.fromCode(compensationReviewBoundaryState),
        automatedDecisionBoundaryState = ReadinessState.fromCode(automatedDecisionBoundaryState),
        compensationSourceDependencyState = ReadinessState.fromCode(compensationSourceDependencyState),
        benefitProviderSourceDependencyState = ReadinessState.fromCode(benefitProviderSourceDependencyState),
        documentDependencyState = ReadinessState.fromCode(documentDependencyState),
        notificationDependencyState = ReadinessState.fromCode(notificationDependencyState),
        consentPreconditionState = ReadinessState.fromCode(consentPreconditionState),
        dataMinimizationState = ReadinessState.fromCode(dataMinimizationState),
        retentionPolicyState = ReadinessState.fromCode(retentionPolicyState),
        evidencePolicyState = ReadinessState.fromCode(evidencePolicyState),
        dependencyStates = dependencyStates.mapValues { ReadinessState.fromCode(it.value) },
        sourceContractVersion = sourceContractVersion,
        compensationBenefitsReadinessVersion = compensationBenefitsReadinessVersion,
        deferredReason = deferredReason,
        lastEvaluatedAt = parseInstant(lastEvaluatedAt),
        syncStatus = syncStatus,
    )

// --- domain (new) -> create request DTO ---------------------------------------

fun NewCompensationBenefits.toCreateRequest(): CompensationBenefitsCreateRequestDto =
    CompensationBenefitsCreateRequestDto(
        code = code,
        displayName = displayName,
        compensationBenefitsReadinessState = compensationBenefitsReadinessState.code,
        compensationPlanBoundaryState = compensationPlanBoundaryState.code,
        benefitProgramBoundaryState = benefitProgramBoundaryState.code,
        payGradeMappingBoundaryState = payGradeMappingBoundaryState.code,
        benefitEnrollmentBoundaryState = benefitEnrollmentBoundaryState.code,
        compensationReviewBoundaryState = compensationReviewBoundaryState.code,
        automatedDecisionBoundaryState = automatedDecisionBoundaryState.code,
        compensationSourceDependencyState = compensationSourceDependencyState.code,
        benefitProviderSourceDependencyState = benefitProviderSourceDependencyState.code,
        documentDependencyState = documentDependencyState.code,
        notificationDependencyState = notificationDependencyState.code,
        consentPreconditionState = consentPreconditionState.code,
        dataMinimizationState = dataMinimizationState.code,
        retentionPolicyState = retentionPolicyState.code,
        evidencePolicyState = evidencePolicyState.code,
        dependencyStates = dependencyStates.mapValues { it.value.code },
        sourceContractVersion = sourceContractVersion,
        compensationBenefitsReadinessVersion = compensationBenefitsReadinessVersion,
        deferredReason = deferredReason,
    )

// --- domain (new) -> entity (local PENDING write) -----------------------------

@Suppress("LongParameterList")
fun NewCompensationBenefits.toEntity(
    id: String,
    scope: ScopeKeys,
    syncStatus: SyncStatus = SyncStatus.PENDING,
): CompensationBenefitsEntity =
    CompensationBenefitsEntity(
        id = id,
        scope = scope,
        code = code,
        displayName = displayName,
        compensationBenefitsReadinessState = compensationBenefitsReadinessState.code,
        compensationPlanBoundaryState = compensationPlanBoundaryState.code,
        benefitProgramBoundaryState = benefitProgramBoundaryState.code,
        payGradeMappingBoundaryState = payGradeMappingBoundaryState.code,
        benefitEnrollmentBoundaryState = benefitEnrollmentBoundaryState.code,
        compensationReviewBoundaryState = compensationReviewBoundaryState.code,
        automatedDecisionBoundaryState = automatedDecisionBoundaryState.code,
        compensationSourceDependencyState = compensationSourceDependencyState.code,
        benefitProviderSourceDependencyState = benefitProviderSourceDependencyState.code,
        documentDependencyState = documentDependencyState.code,
        notificationDependencyState = notificationDependencyState.code,
        consentPreconditionState = consentPreconditionState.code,
        dataMinimizationState = dataMinimizationState.code,
        retentionPolicyState = retentionPolicyState.code,
        evidencePolicyState = evidencePolicyState.code,
        dependencyStates = dependencyStates.mapValues { it.value.code },
        sourceContractVersion = sourceContractVersion,
        compensationBenefitsReadinessVersion = compensationBenefitsReadinessVersion,
        deferredReason = deferredReason,
        lastEvaluatedAt = null,
        syncStatus = syncStatus,
    )

/** Entity (a PENDING local row) -> create request, for the sync push. */
fun CompensationBenefitsEntity.toCreateRequest(): CompensationBenefitsCreateRequestDto =
    CompensationBenefitsCreateRequestDto(
        code = code,
        displayName = displayName,
        compensationBenefitsReadinessState = compensationBenefitsReadinessState,
        compensationPlanBoundaryState = compensationPlanBoundaryState,
        benefitProgramBoundaryState = benefitProgramBoundaryState,
        payGradeMappingBoundaryState = payGradeMappingBoundaryState,
        benefitEnrollmentBoundaryState = benefitEnrollmentBoundaryState,
        compensationReviewBoundaryState = compensationReviewBoundaryState,
        automatedDecisionBoundaryState = automatedDecisionBoundaryState,
        compensationSourceDependencyState = compensationSourceDependencyState,
        benefitProviderSourceDependencyState = benefitProviderSourceDependencyState,
        documentDependencyState = documentDependencyState,
        notificationDependencyState = notificationDependencyState,
        consentPreconditionState = consentPreconditionState,
        dataMinimizationState = dataMinimizationState,
        retentionPolicyState = retentionPolicyState,
        evidencePolicyState = evidencePolicyState,
        dependencyStates = dependencyStates,
        sourceContractVersion = sourceContractVersion,
        compensationBenefitsReadinessVersion = compensationBenefitsReadinessVersion,
        deferredReason = deferredReason,
    )

// --- DTO -> entity (server row cached as SYNCED) ------------------------------

fun CompensationBenefitsReadinessDto.toEntity(scope: ScopeKeys): CompensationBenefitsEntity =
    CompensationBenefitsEntity(
        id = id,
        scope = scope,
        code = code,
        displayName = displayName,
        compensationBenefitsReadinessState = compensationBenefitsReadinessState,
        compensationPlanBoundaryState = compensationPlanBoundaryState,
        benefitProgramBoundaryState = benefitProgramBoundaryState,
        payGradeMappingBoundaryState = payGradeMappingBoundaryState,
        benefitEnrollmentBoundaryState = benefitEnrollmentBoundaryState,
        compensationReviewBoundaryState = compensationReviewBoundaryState,
        automatedDecisionBoundaryState = automatedDecisionBoundaryState,
        compensationSourceDependencyState = compensationSourceDependencyState,
        benefitProviderSourceDependencyState = benefitProviderSourceDependencyState,
        documentDependencyState = documentDependencyState,
        notificationDependencyState = notificationDependencyState,
        consentPreconditionState = consentPreconditionState,
        dataMinimizationState = dataMinimizationState,
        retentionPolicyState = retentionPolicyState,
        evidencePolicyState = evidencePolicyState,
        dependencyStates = dependencyStates,
        sourceContractVersion = sourceContractVersion,
        compensationBenefitsReadinessVersion = compensationBenefitsReadinessVersion,
        deferredReason = deferredReason,
        lastEvaluatedAt = parseInstant(lastEvaluatedAt),
        syncStatus = SyncStatus.SYNCED,
    )

/**
 * List item DTO -> entity. The list payload carries only the top-level state and
 * four representative facets, so the omitted facets are seeded with backend
 * defaults (Blocked=3 boundaries, Deferred=2 dependencies/policies); a later
 * detail fetch/evaluate fills in the authoritative values.
 */
fun CompensationBenefitsReadinessListItemDto.toEntity(scope: ScopeKeys): CompensationBenefitsEntity =
    CompensationBenefitsEntity(
        id = id,
        scope = scope,
        code = code,
        displayName = displayName,
        compensationBenefitsReadinessState = compensationBenefitsReadinessState,
        compensationPlanBoundaryState = compensationPlanBoundaryState,
        benefitProgramBoundaryState = ReadinessState.Blocked.code,
        payGradeMappingBoundaryState = ReadinessState.Blocked.code,
        benefitEnrollmentBoundaryState = benefitEnrollmentBoundaryState,
        compensationReviewBoundaryState = ReadinessState.Blocked.code,
        automatedDecisionBoundaryState = automatedDecisionBoundaryState,
        compensationSourceDependencyState = compensationSourceDependencyState,
        benefitProviderSourceDependencyState = ReadinessState.Deferred.code,
        documentDependencyState = ReadinessState.Deferred.code,
        notificationDependencyState = ReadinessState.Deferred.code,
        consentPreconditionState = ReadinessState.Deferred.code,
        dataMinimizationState = ReadinessState.Deferred.code,
        retentionPolicyState = ReadinessState.Deferred.code,
        evidencePolicyState = ReadinessState.Deferred.code,
        dependencyStates = emptyMap(),
        sourceContractVersion = sourceContractVersion,
        compensationBenefitsReadinessVersion = 1L,
        deferredReason = null,
        lastEvaluatedAt = parseInstant(lastEvaluatedAt),
        syncStatus = SyncStatus.SYNCED,
    )

// --- entity -> domain ---------------------------------------------------------

fun CompensationBenefitsEntity.toDomain(): CompensationBenefitsReadiness =
    CompensationBenefitsReadiness(
        id = id,
        code = code,
        displayName = displayName,
        compensationBenefitsReadinessState = ReadinessState.fromCode(compensationBenefitsReadinessState),
        compensationPlanBoundaryState = ReadinessState.fromCode(compensationPlanBoundaryState),
        benefitProgramBoundaryState = ReadinessState.fromCode(benefitProgramBoundaryState),
        payGradeMappingBoundaryState = ReadinessState.fromCode(payGradeMappingBoundaryState),
        benefitEnrollmentBoundaryState = ReadinessState.fromCode(benefitEnrollmentBoundaryState),
        compensationReviewBoundaryState = ReadinessState.fromCode(compensationReviewBoundaryState),
        automatedDecisionBoundaryState = ReadinessState.fromCode(automatedDecisionBoundaryState),
        compensationSourceDependencyState = ReadinessState.fromCode(compensationSourceDependencyState),
        benefitProviderSourceDependencyState = ReadinessState.fromCode(benefitProviderSourceDependencyState),
        documentDependencyState = ReadinessState.fromCode(documentDependencyState),
        notificationDependencyState = ReadinessState.fromCode(notificationDependencyState),
        consentPreconditionState = ReadinessState.fromCode(consentPreconditionState),
        dataMinimizationState = ReadinessState.fromCode(dataMinimizationState),
        retentionPolicyState = ReadinessState.fromCode(retentionPolicyState),
        evidencePolicyState = ReadinessState.fromCode(evidencePolicyState),
        dependencyStates = dependencyStates.mapValues { ReadinessState.fromCode(it.value) },
        sourceContractVersion = sourceContractVersion,
        compensationBenefitsReadinessVersion = compensationBenefitsReadinessVersion,
        deferredReason = deferredReason,
        lastEvaluatedAt = lastEvaluatedAt,
        syncStatus = syncStatus,
    )

fun CompensationBenefitsEntity.toListItem(): CompensationBenefitsListItem =
    CompensationBenefitsListItem(
        id = id,
        code = code,
        displayName = displayName,
        compensationBenefitsReadinessState = ReadinessState.fromCode(compensationBenefitsReadinessState),
        sourceContractVersion = sourceContractVersion,
        lastEvaluatedAt = lastEvaluatedAt,
        syncStatus = syncStatus,
    )
