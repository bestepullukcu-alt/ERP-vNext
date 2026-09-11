package eu.grandmedical.diten.mobile.feature.offermanagement.data.mapper

import eu.grandmedical.diten.mobile.core.database.ScopeKeys
import eu.grandmedical.diten.mobile.core.database.entity.SyncStatus
import eu.grandmedical.diten.mobile.feature.offermanagement.data.dto.OfferManagementCreateRequestDto
import eu.grandmedical.diten.mobile.feature.offermanagement.data.dto.OfferManagementReadinessDto
import eu.grandmedical.diten.mobile.feature.offermanagement.data.dto.OfferManagementReadinessListItemDto
import eu.grandmedical.diten.mobile.feature.offermanagement.data.local.OfferManagementEntity
import eu.grandmedical.diten.mobile.feature.offermanagement.domain.NewOfferManagement
import eu.grandmedical.diten.mobile.feature.offermanagement.domain.OfferManagementListItem
import eu.grandmedical.diten.mobile.feature.offermanagement.domain.OfferManagementReadiness
import eu.grandmedical.diten.mobile.feature.offermanagement.domain.ReadinessState
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

fun OfferManagementReadinessDto.toDomain(syncStatus: SyncStatus = SyncStatus.SYNCED): OfferManagementReadiness =
    OfferManagementReadiness(
        id = id,
        code = code,
        displayName = displayName,
        offerReadinessState = ReadinessState.fromCode(offerReadinessState),
        offerWorkflowBoundaryState = ReadinessState.fromCode(offerWorkflowBoundaryState),
        approvalWorkflowBoundaryState = ReadinessState.fromCode(approvalWorkflowBoundaryState),
        candidateAcceptanceBoundaryState = ReadinessState.fromCode(candidateAcceptanceBoundaryState),
        offerDocumentBoundaryState = ReadinessState.fromCode(offerDocumentBoundaryState),
        compensationDataBoundaryState = ReadinessState.fromCode(compensationDataBoundaryState),
        benefitsDataBoundaryState = ReadinessState.fromCode(benefitsDataBoundaryState),
        payrollDataBoundaryState = ReadinessState.fromCode(payrollDataBoundaryState),
        consentPreconditionState = ReadinessState.fromCode(consentPreconditionState),
        dataMinimizationState = ReadinessState.fromCode(dataMinimizationState),
        retentionPolicyState = ReadinessState.fromCode(retentionPolicyState),
        evidencePolicyState = ReadinessState.fromCode(evidencePolicyState),
        notificationDependencyState = ReadinessState.fromCode(notificationDependencyState),
        documentDependencyState = ReadinessState.fromCode(documentDependencyState),
        dependencyStates = dependencyStates.mapValues { ReadinessState.fromCode(it.value) },
        sourceContractVersion = sourceContractVersion,
        offerReadinessVersion = offerReadinessVersion,
        deferredReason = deferredReason,
        lastEvaluatedAt = parseInstant(lastEvaluatedAt),
        syncStatus = syncStatus,
    )

// --- domain (new) -> create request DTO ---------------------------------------

fun NewOfferManagement.toCreateRequest(): OfferManagementCreateRequestDto =
    OfferManagementCreateRequestDto(
        code = code,
        displayName = displayName,
        offerReadinessState = offerReadinessState.code,
        offerWorkflowBoundaryState = offerWorkflowBoundaryState.code,
        approvalWorkflowBoundaryState = approvalWorkflowBoundaryState.code,
        candidateAcceptanceBoundaryState = candidateAcceptanceBoundaryState.code,
        offerDocumentBoundaryState = offerDocumentBoundaryState.code,
        compensationDataBoundaryState = compensationDataBoundaryState.code,
        benefitsDataBoundaryState = benefitsDataBoundaryState.code,
        payrollDataBoundaryState = payrollDataBoundaryState.code,
        consentPreconditionState = consentPreconditionState.code,
        dataMinimizationState = dataMinimizationState.code,
        retentionPolicyState = retentionPolicyState.code,
        evidencePolicyState = evidencePolicyState.code,
        notificationDependencyState = notificationDependencyState.code,
        documentDependencyState = documentDependencyState.code,
        dependencyStates = dependencyStates.mapValues { it.value.code },
        sourceContractVersion = sourceContractVersion,
        offerReadinessVersion = offerReadinessVersion,
        deferredReason = deferredReason,
    )

// --- domain (new) -> entity (local PENDING write) -----------------------------

@Suppress("LongParameterList")
fun NewOfferManagement.toEntity(
    id: String,
    scope: ScopeKeys,
    syncStatus: SyncStatus = SyncStatus.PENDING,
): OfferManagementEntity =
    OfferManagementEntity(
        id = id,
        scope = scope,
        code = code,
        displayName = displayName,
        offerReadinessState = offerReadinessState.code,
        offerWorkflowBoundaryState = offerWorkflowBoundaryState.code,
        approvalWorkflowBoundaryState = approvalWorkflowBoundaryState.code,
        candidateAcceptanceBoundaryState = candidateAcceptanceBoundaryState.code,
        offerDocumentBoundaryState = offerDocumentBoundaryState.code,
        compensationDataBoundaryState = compensationDataBoundaryState.code,
        benefitsDataBoundaryState = benefitsDataBoundaryState.code,
        payrollDataBoundaryState = payrollDataBoundaryState.code,
        consentPreconditionState = consentPreconditionState.code,
        dataMinimizationState = dataMinimizationState.code,
        retentionPolicyState = retentionPolicyState.code,
        evidencePolicyState = evidencePolicyState.code,
        notificationDependencyState = notificationDependencyState.code,
        documentDependencyState = documentDependencyState.code,
        dependencyStates = dependencyStates.mapValues { it.value.code },
        sourceContractVersion = sourceContractVersion,
        offerReadinessVersion = offerReadinessVersion,
        deferredReason = deferredReason,
        lastEvaluatedAt = null,
        syncStatus = syncStatus,
    )

/** Entity (a PENDING local row) -> create request, for the sync push. */
fun OfferManagementEntity.toCreateRequest(): OfferManagementCreateRequestDto =
    OfferManagementCreateRequestDto(
        code = code,
        displayName = displayName,
        offerReadinessState = offerReadinessState,
        offerWorkflowBoundaryState = offerWorkflowBoundaryState,
        approvalWorkflowBoundaryState = approvalWorkflowBoundaryState,
        candidateAcceptanceBoundaryState = candidateAcceptanceBoundaryState,
        offerDocumentBoundaryState = offerDocumentBoundaryState,
        compensationDataBoundaryState = compensationDataBoundaryState,
        benefitsDataBoundaryState = benefitsDataBoundaryState,
        payrollDataBoundaryState = payrollDataBoundaryState,
        consentPreconditionState = consentPreconditionState,
        dataMinimizationState = dataMinimizationState,
        retentionPolicyState = retentionPolicyState,
        evidencePolicyState = evidencePolicyState,
        notificationDependencyState = notificationDependencyState,
        documentDependencyState = documentDependencyState,
        dependencyStates = dependencyStates,
        sourceContractVersion = sourceContractVersion,
        offerReadinessVersion = offerReadinessVersion,
        deferredReason = deferredReason,
    )

// --- DTO -> entity (server row cached as SYNCED) ------------------------------

fun OfferManagementReadinessDto.toEntity(scope: ScopeKeys): OfferManagementEntity =
    OfferManagementEntity(
        id = id,
        scope = scope,
        code = code,
        displayName = displayName,
        offerReadinessState = offerReadinessState,
        offerWorkflowBoundaryState = offerWorkflowBoundaryState,
        approvalWorkflowBoundaryState = approvalWorkflowBoundaryState,
        candidateAcceptanceBoundaryState = candidateAcceptanceBoundaryState,
        offerDocumentBoundaryState = offerDocumentBoundaryState,
        compensationDataBoundaryState = compensationDataBoundaryState,
        benefitsDataBoundaryState = benefitsDataBoundaryState,
        payrollDataBoundaryState = payrollDataBoundaryState,
        consentPreconditionState = consentPreconditionState,
        dataMinimizationState = dataMinimizationState,
        retentionPolicyState = retentionPolicyState,
        evidencePolicyState = evidencePolicyState,
        notificationDependencyState = notificationDependencyState,
        documentDependencyState = documentDependencyState,
        dependencyStates = dependencyStates,
        sourceContractVersion = sourceContractVersion,
        offerReadinessVersion = offerReadinessVersion,
        deferredReason = deferredReason,
        lastEvaluatedAt = parseInstant(lastEvaluatedAt),
        syncStatus = SyncStatus.SYNCED,
    )

/**
 * List item DTO -> entity. The list payload omits most facet states, so those
 * columns are seeded with backend defaults; a later detail fetch/evaluate fills
 * in the authoritative values.
 */
fun OfferManagementReadinessListItemDto.toEntity(scope: ScopeKeys): OfferManagementEntity =
    OfferManagementEntity(
        id = id,
        scope = scope,
        code = code,
        displayName = displayName,
        offerReadinessState = offerReadinessState,
        offerWorkflowBoundaryState = offerWorkflowBoundaryState,
        approvalWorkflowBoundaryState = approvalWorkflowBoundaryState,
        candidateAcceptanceBoundaryState = candidateAcceptanceBoundaryState,
        offerDocumentBoundaryState = ReadinessState.Blocked.code,
        compensationDataBoundaryState = ReadinessState.Deferred.code,
        benefitsDataBoundaryState = ReadinessState.Deferred.code,
        payrollDataBoundaryState = ReadinessState.Deferred.code,
        consentPreconditionState = ReadinessState.Deferred.code,
        dataMinimizationState = ReadinessState.Deferred.code,
        retentionPolicyState = ReadinessState.Deferred.code,
        evidencePolicyState = ReadinessState.Deferred.code,
        notificationDependencyState = ReadinessState.Deferred.code,
        documentDependencyState = ReadinessState.Deferred.code,
        dependencyStates = emptyMap(),
        sourceContractVersion = sourceContractVersion,
        offerReadinessVersion = 1L,
        deferredReason = null,
        lastEvaluatedAt = parseInstant(lastEvaluatedAt),
        syncStatus = SyncStatus.SYNCED,
    )

// --- entity -> domain ---------------------------------------------------------

fun OfferManagementEntity.toDomain(): OfferManagementReadiness =
    OfferManagementReadiness(
        id = id,
        code = code,
        displayName = displayName,
        offerReadinessState = ReadinessState.fromCode(offerReadinessState),
        offerWorkflowBoundaryState = ReadinessState.fromCode(offerWorkflowBoundaryState),
        approvalWorkflowBoundaryState = ReadinessState.fromCode(approvalWorkflowBoundaryState),
        candidateAcceptanceBoundaryState = ReadinessState.fromCode(candidateAcceptanceBoundaryState),
        offerDocumentBoundaryState = ReadinessState.fromCode(offerDocumentBoundaryState),
        compensationDataBoundaryState = ReadinessState.fromCode(compensationDataBoundaryState),
        benefitsDataBoundaryState = ReadinessState.fromCode(benefitsDataBoundaryState),
        payrollDataBoundaryState = ReadinessState.fromCode(payrollDataBoundaryState),
        consentPreconditionState = ReadinessState.fromCode(consentPreconditionState),
        dataMinimizationState = ReadinessState.fromCode(dataMinimizationState),
        retentionPolicyState = ReadinessState.fromCode(retentionPolicyState),
        evidencePolicyState = ReadinessState.fromCode(evidencePolicyState),
        notificationDependencyState = ReadinessState.fromCode(notificationDependencyState),
        documentDependencyState = ReadinessState.fromCode(documentDependencyState),
        dependencyStates = dependencyStates.mapValues { ReadinessState.fromCode(it.value) },
        sourceContractVersion = sourceContractVersion,
        offerReadinessVersion = offerReadinessVersion,
        deferredReason = deferredReason,
        lastEvaluatedAt = lastEvaluatedAt,
        syncStatus = syncStatus,
    )

fun OfferManagementEntity.toListItem(): OfferManagementListItem =
    OfferManagementListItem(
        id = id,
        code = code,
        displayName = displayName,
        offerReadinessState = ReadinessState.fromCode(offerReadinessState),
        sourceContractVersion = sourceContractVersion,
        lastEvaluatedAt = lastEvaluatedAt,
        syncStatus = syncStatus,
    )
