package eu.grandmedical.diten.mobile.feature.employeeonboarding.data.mapper

import eu.grandmedical.diten.mobile.core.database.ScopeKeys
import eu.grandmedical.diten.mobile.core.database.entity.SyncStatus
import eu.grandmedical.diten.mobile.feature.employeeonboarding.data.dto.EmployeeOnboardingCreateRequestDto
import eu.grandmedical.diten.mobile.feature.employeeonboarding.data.dto.EmployeeOnboardingReadinessDto
import eu.grandmedical.diten.mobile.feature.employeeonboarding.data.dto.EmployeeOnboardingReadinessListItemDto
import eu.grandmedical.diten.mobile.feature.employeeonboarding.data.local.EmployeeOnboardingEntity
import eu.grandmedical.diten.mobile.feature.employeeonboarding.domain.EmployeeOnboardingListItem
import eu.grandmedical.diten.mobile.feature.employeeonboarding.domain.EmployeeOnboardingReadiness
import eu.grandmedical.diten.mobile.feature.employeeonboarding.domain.NewEmployeeOnboarding
import eu.grandmedical.diten.mobile.feature.employeeonboarding.domain.ReadinessState
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

fun EmployeeOnboardingReadinessDto.toDomain(syncStatus: SyncStatus = SyncStatus.SYNCED): EmployeeOnboardingReadiness =
    EmployeeOnboardingReadiness(
        id = id,
        code = code,
        displayName = displayName,
        onboardingReadinessState = ReadinessState.fromCode(onboardingReadinessState),
        lifecycleBoundaryState = ReadinessState.fromCode(lifecycleBoundaryState),
        checklistBoundaryState = ReadinessState.fromCode(checklistBoundaryState),
        managerActionBoundaryState = ReadinessState.fromCode(managerActionBoundaryState),
        employeeActionBoundaryState = ReadinessState.fromCode(employeeActionBoundaryState),
        candidateTransitionBoundaryState = ReadinessState.fromCode(candidateTransitionBoundaryState),
        identityProvisioningBoundaryState = ReadinessState.fromCode(identityProvisioningBoundaryState),
        accessProvisioningBoundaryState = ReadinessState.fromCode(accessProvisioningBoundaryState),
        deviceEquipmentProvisioningBoundaryState = ReadinessState.fromCode(deviceEquipmentProvisioningBoundaryState),
        documentDependencyState = ReadinessState.fromCode(documentDependencyState),
        notificationDependencyState = ReadinessState.fromCode(notificationDependencyState),
        consentPreconditionState = ReadinessState.fromCode(consentPreconditionState),
        dataMinimizationState = ReadinessState.fromCode(dataMinimizationState),
        retentionPolicyState = ReadinessState.fromCode(retentionPolicyState),
        evidencePolicyState = ReadinessState.fromCode(evidencePolicyState),
        dependencyStates = dependencyStates.mapValues { ReadinessState.fromCode(it.value) },
        sourceContractVersion = sourceContractVersion,
        onboardingReadinessVersion = onboardingReadinessVersion,
        deferredReason = deferredReason,
        lastEvaluatedAt = parseInstant(lastEvaluatedAt),
        syncStatus = syncStatus,
    )

// --- domain (new) -> create request DTO ---------------------------------------

fun NewEmployeeOnboarding.toCreateRequest(): EmployeeOnboardingCreateRequestDto =
    EmployeeOnboardingCreateRequestDto(
        code = code,
        displayName = displayName,
        onboardingReadinessState = onboardingReadinessState.code,
        lifecycleBoundaryState = lifecycleBoundaryState.code,
        checklistBoundaryState = checklistBoundaryState.code,
        managerActionBoundaryState = managerActionBoundaryState.code,
        employeeActionBoundaryState = employeeActionBoundaryState.code,
        candidateTransitionBoundaryState = candidateTransitionBoundaryState.code,
        identityProvisioningBoundaryState = identityProvisioningBoundaryState.code,
        accessProvisioningBoundaryState = accessProvisioningBoundaryState.code,
        deviceEquipmentProvisioningBoundaryState = deviceEquipmentProvisioningBoundaryState.code,
        documentDependencyState = documentDependencyState.code,
        notificationDependencyState = notificationDependencyState.code,
        consentPreconditionState = consentPreconditionState.code,
        dataMinimizationState = dataMinimizationState.code,
        retentionPolicyState = retentionPolicyState.code,
        evidencePolicyState = evidencePolicyState.code,
        dependencyStates = dependencyStates.mapValues { it.value.code },
        sourceContractVersion = sourceContractVersion,
        onboardingReadinessVersion = onboardingReadinessVersion,
        deferredReason = deferredReason,
    )

// --- domain (new) -> entity (local PENDING write) -----------------------------

@Suppress("LongParameterList")
fun NewEmployeeOnboarding.toEntity(
    id: String,
    scope: ScopeKeys,
    syncStatus: SyncStatus = SyncStatus.PENDING,
): EmployeeOnboardingEntity =
    EmployeeOnboardingEntity(
        id = id,
        scope = scope,
        code = code,
        displayName = displayName,
        onboardingReadinessState = onboardingReadinessState.code,
        lifecycleBoundaryState = lifecycleBoundaryState.code,
        checklistBoundaryState = checklistBoundaryState.code,
        managerActionBoundaryState = managerActionBoundaryState.code,
        employeeActionBoundaryState = employeeActionBoundaryState.code,
        candidateTransitionBoundaryState = candidateTransitionBoundaryState.code,
        identityProvisioningBoundaryState = identityProvisioningBoundaryState.code,
        accessProvisioningBoundaryState = accessProvisioningBoundaryState.code,
        deviceEquipmentProvisioningBoundaryState = deviceEquipmentProvisioningBoundaryState.code,
        documentDependencyState = documentDependencyState.code,
        notificationDependencyState = notificationDependencyState.code,
        consentPreconditionState = consentPreconditionState.code,
        dataMinimizationState = dataMinimizationState.code,
        retentionPolicyState = retentionPolicyState.code,
        evidencePolicyState = evidencePolicyState.code,
        dependencyStates = dependencyStates.mapValues { it.value.code },
        sourceContractVersion = sourceContractVersion,
        onboardingReadinessVersion = onboardingReadinessVersion,
        deferredReason = deferredReason,
        lastEvaluatedAt = null,
        syncStatus = syncStatus,
    )

/** Entity (a PENDING local row) -> create request, for the sync push. */
fun EmployeeOnboardingEntity.toCreateRequest(): EmployeeOnboardingCreateRequestDto =
    EmployeeOnboardingCreateRequestDto(
        code = code,
        displayName = displayName,
        onboardingReadinessState = onboardingReadinessState,
        lifecycleBoundaryState = lifecycleBoundaryState,
        checklistBoundaryState = checklistBoundaryState,
        managerActionBoundaryState = managerActionBoundaryState,
        employeeActionBoundaryState = employeeActionBoundaryState,
        candidateTransitionBoundaryState = candidateTransitionBoundaryState,
        identityProvisioningBoundaryState = identityProvisioningBoundaryState,
        accessProvisioningBoundaryState = accessProvisioningBoundaryState,
        deviceEquipmentProvisioningBoundaryState = deviceEquipmentProvisioningBoundaryState,
        documentDependencyState = documentDependencyState,
        notificationDependencyState = notificationDependencyState,
        consentPreconditionState = consentPreconditionState,
        dataMinimizationState = dataMinimizationState,
        retentionPolicyState = retentionPolicyState,
        evidencePolicyState = evidencePolicyState,
        dependencyStates = dependencyStates,
        sourceContractVersion = sourceContractVersion,
        onboardingReadinessVersion = onboardingReadinessVersion,
        deferredReason = deferredReason,
    )

// --- DTO -> entity (server row cached as SYNCED) ------------------------------

fun EmployeeOnboardingReadinessDto.toEntity(scope: ScopeKeys): EmployeeOnboardingEntity =
    EmployeeOnboardingEntity(
        id = id,
        scope = scope,
        code = code,
        displayName = displayName,
        onboardingReadinessState = onboardingReadinessState,
        lifecycleBoundaryState = lifecycleBoundaryState,
        checklistBoundaryState = checklistBoundaryState,
        managerActionBoundaryState = managerActionBoundaryState,
        employeeActionBoundaryState = employeeActionBoundaryState,
        candidateTransitionBoundaryState = candidateTransitionBoundaryState,
        identityProvisioningBoundaryState = identityProvisioningBoundaryState,
        accessProvisioningBoundaryState = accessProvisioningBoundaryState,
        deviceEquipmentProvisioningBoundaryState = deviceEquipmentProvisioningBoundaryState,
        documentDependencyState = documentDependencyState,
        notificationDependencyState = notificationDependencyState,
        consentPreconditionState = consentPreconditionState,
        dataMinimizationState = dataMinimizationState,
        retentionPolicyState = retentionPolicyState,
        evidencePolicyState = evidencePolicyState,
        dependencyStates = dependencyStates,
        sourceContractVersion = sourceContractVersion,
        onboardingReadinessVersion = onboardingReadinessVersion,
        deferredReason = deferredReason,
        lastEvaluatedAt = parseInstant(lastEvaluatedAt),
        syncStatus = SyncStatus.SYNCED,
    )

/**
 * List item DTO -> entity. The list payload carries only the lifecycle / checklist
 * / manager-action boundaries, so the remaining columns are seeded with backend
 * defaults; a later detail fetch/evaluate fills in the authoritative values.
 */
fun EmployeeOnboardingReadinessListItemDto.toEntity(scope: ScopeKeys): EmployeeOnboardingEntity =
    EmployeeOnboardingEntity(
        id = id,
        scope = scope,
        code = code,
        displayName = displayName,
        onboardingReadinessState = onboardingReadinessState,
        lifecycleBoundaryState = lifecycleBoundaryState,
        checklistBoundaryState = checklistBoundaryState,
        managerActionBoundaryState = managerActionBoundaryState,
        employeeActionBoundaryState = ReadinessState.Blocked.code,
        candidateTransitionBoundaryState = ReadinessState.Deferred.code,
        identityProvisioningBoundaryState = ReadinessState.Deferred.code,
        accessProvisioningBoundaryState = ReadinessState.Deferred.code,
        deviceEquipmentProvisioningBoundaryState = ReadinessState.Deferred.code,
        documentDependencyState = ReadinessState.Deferred.code,
        notificationDependencyState = ReadinessState.Deferred.code,
        consentPreconditionState = ReadinessState.Deferred.code,
        dataMinimizationState = ReadinessState.Deferred.code,
        retentionPolicyState = ReadinessState.Deferred.code,
        evidencePolicyState = ReadinessState.Deferred.code,
        dependencyStates = emptyMap(),
        sourceContractVersion = sourceContractVersion,
        onboardingReadinessVersion = 1L,
        deferredReason = null,
        lastEvaluatedAt = parseInstant(lastEvaluatedAt),
        syncStatus = SyncStatus.SYNCED,
    )

// --- entity -> domain ---------------------------------------------------------

fun EmployeeOnboardingEntity.toDomain(): EmployeeOnboardingReadiness =
    EmployeeOnboardingReadiness(
        id = id,
        code = code,
        displayName = displayName,
        onboardingReadinessState = ReadinessState.fromCode(onboardingReadinessState),
        lifecycleBoundaryState = ReadinessState.fromCode(lifecycleBoundaryState),
        checklistBoundaryState = ReadinessState.fromCode(checklistBoundaryState),
        managerActionBoundaryState = ReadinessState.fromCode(managerActionBoundaryState),
        employeeActionBoundaryState = ReadinessState.fromCode(employeeActionBoundaryState),
        candidateTransitionBoundaryState = ReadinessState.fromCode(candidateTransitionBoundaryState),
        identityProvisioningBoundaryState = ReadinessState.fromCode(identityProvisioningBoundaryState),
        accessProvisioningBoundaryState = ReadinessState.fromCode(accessProvisioningBoundaryState),
        deviceEquipmentProvisioningBoundaryState = ReadinessState.fromCode(deviceEquipmentProvisioningBoundaryState),
        documentDependencyState = ReadinessState.fromCode(documentDependencyState),
        notificationDependencyState = ReadinessState.fromCode(notificationDependencyState),
        consentPreconditionState = ReadinessState.fromCode(consentPreconditionState),
        dataMinimizationState = ReadinessState.fromCode(dataMinimizationState),
        retentionPolicyState = ReadinessState.fromCode(retentionPolicyState),
        evidencePolicyState = ReadinessState.fromCode(evidencePolicyState),
        dependencyStates = dependencyStates.mapValues { ReadinessState.fromCode(it.value) },
        sourceContractVersion = sourceContractVersion,
        onboardingReadinessVersion = onboardingReadinessVersion,
        deferredReason = deferredReason,
        lastEvaluatedAt = lastEvaluatedAt,
        syncStatus = syncStatus,
    )

fun EmployeeOnboardingEntity.toListItem(): EmployeeOnboardingListItem =
    EmployeeOnboardingListItem(
        id = id,
        code = code,
        displayName = displayName,
        onboardingReadinessState = ReadinessState.fromCode(onboardingReadinessState),
        sourceContractVersion = sourceContractVersion,
        lastEvaluatedAt = lastEvaluatedAt,
        syncStatus = syncStatus,
    )
